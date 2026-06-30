// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DoAfter;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Beekeeping;


public sealed class BeeAISystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeeComponent, BeePollinatingDoAfterEvent>(OnPollinatingDoAfter);
        SubscribeLocalEvent<BeeComponent, BeeDepositingDoAfterEvent>(OnDepositingDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BeeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bee, out var xform))
        {
            // Полностью блокируем обновление пчелы, если она занята DoAfter
            if (bee.IsBusy)
                continue;

            bee.StateTimer += frameTime;
            TickState(uid, bee, xform, frameTime);
        }
    }

    /// <summary>
    /// Отменяет все активные DoAfter у пчелы и снимает флаг занятости.
    /// </summary>
    private void CancelActiveDoAfters(EntityUid uid, BeeComponent bee)
    {
        if (!TryComp<DoAfterComponent>(uid, out var doAfterComp))
            return;

        foreach (var doAfter in doAfterComp.DoAfters.Values)
        {
            if (!doAfter.Cancelled && !doAfter.Completed)
            {
                _doAfter.Cancel(doAfter.Id);
            }
        }

        bee.IsBusy = false;
    }

    private void TickState(EntityUid uid, BeeComponent bee, TransformComponent xform, float frameTime)
    {
        switch (bee.State)
        {
            case BeeState.Idle: TickIdle(uid, bee, xform); break;
            case BeeState.SearchingFlower: TickSearching(uid, bee, xform); break;
            case BeeState.MovingToFlower: TickMoving(uid, bee, xform, frameTime); break;
            case BeeState.Pollinating: TickPollinating(uid, bee, xform); break;
            case BeeState.ReturningToHive: TickReturning(uid, bee, xform, frameTime); break;
            case BeeState.DepositingPollen: TickDepositing(uid, bee, xform); break;
        }
    }

    private void SetState(EntityUid uid, BeeComponent bee, BeeState newState)
    {
        // Если уходим из состояния с DoAfter — отменяем DoAfter и снимаем занятость
        if ((bee.State == BeeState.Pollinating || bee.State == BeeState.DepositingPollen) 
            && newState != bee.State)
        {
            CancelActiveDoAfters(uid, bee);
        }

        bee.State = newState;
        bee.StateTimer = 0f;
    }

    private void TickIdle(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (bee.StateTimer < bee.IdleCooldown) return;

        if (bee.HiveOwner == null || !EntityManager.EntityExists(bee.HiveOwner.Value))
        {
            return;
        }

        SetState(uid, bee, BeeState.SearchingFlower);
    }

    private void TickSearching(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        var worldPos = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        float bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<PollinationComponent, TransformComponent>();
        while (query.MoveNext(out var flowerUid, out var poll, out var flowerXform))
        {
            if (!poll.IsFlowering || poll.WasPollinated) continue;

            var dist = (_transform.GetWorldPosition(flowerXform) - worldPos).Length();
            if (dist < bee.SearchRadius && dist < bestDist)
            {
                bestDist = dist;
                best = flowerUid;
            }
        }

        if (best == null)
        {
            SetState(uid, bee, BeeState.Idle);
            return;
        }

        bee.TargetFlower = best;
        SetState(uid, bee, BeeState.MovingToFlower);
    }

    private void TickMoving(EntityUid uid, BeeComponent bee, TransformComponent xform, float frameTime)
    {
        if (bee.TargetFlower == null || !EntityManager.EntityExists(bee.TargetFlower.Value))
        {
            bee.TargetFlower = null;
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        var targetPos = _transform.GetWorldPosition(bee.TargetFlower.Value);
        var myPos = _transform.GetWorldPosition(uid);
        var dist = (targetPos - myPos).Length();

        if (dist < bee.ArrivalThreshold)
        {
            SetState(uid, bee, BeeState.Pollinating);
            return;
        }

        var direction = (targetPos - myPos).Normalized();
        var speed = 3f;
        var movement = direction * speed * frameTime;
        _transform.SetWorldPosition(uid, myPos + movement);
    }

    private void TickPollinating(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (bee.TargetFlower == null || !EntityManager.EntityExists(bee.TargetFlower.Value))
        {
            bee.TargetFlower = null;
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        // Проверяем, что пчела всё ещё рядом с цветком
        var targetPos = _transform.GetWorldPosition(bee.TargetFlower.Value);
        var myPos = _transform.GetWorldPosition(uid);
        var dist = (targetPos - myPos).Length();

        if (dist > bee.ArrivalThreshold * 2f)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (!TryComp<PollinationComponent>(bee.TargetFlower.Value, out var pollination))
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        var canPollinateEv = new CanPollinateEvent();
        RaiseLocalEvent(bee.TargetFlower.Value, ref canPollinateEv);

        if (!canPollinateEv.CanPollinate)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        // Если уже заняты — не запускаем новый DoAfter
        if (bee.IsBusy)
            return;

        // Запускаем DoAfter и блокируем движение
        bee.IsBusy = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(2), new BeePollinatingDoAfterEvent(), bee.TargetFlower.Value)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            EventTarget = uid,
            BlockDuplicate = true,
        };

        // Если не удалось запустить DoAfter — снимаем занятость
        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            bee.IsBusy = false;
            SetState(uid, bee, BeeState.SearchingFlower);
        }
    }

    private void OnPollinatingDoAfter(EntityUid uid, BeeComponent bee, BeePollinatingDoAfterEvent args)
    {
        // В любом случае снимаем занятость — DoAfter завершился
        bee.IsBusy = false;

        // Проверяем, был ли DoAfter отменён
        if (args.DoAfter is { } doAfter && doAfter.CancelledTime != null)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (bee.TargetFlower == null || !EntityManager.EntityExists(bee.TargetFlower.Value))
        {
            bee.TargetFlower = null;
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (!TryComp<PollinationComponent>(bee.TargetFlower.Value, out var pollination))
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        var canPollinateEv = new CanPollinateEvent();
        RaiseLocalEvent(bee.TargetFlower.Value, ref canPollinateEv);

        if (!canPollinateEv.CanPollinate)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        // Собираем пыльцу
        var pollenToCollect = Math.Min(
            pollination.PollenYield,
            bee.MaxPollenCarry - bee.PollenCarried);

        if (pollenToCollect > 0)
        {
            bee.PollenCarried += pollenToCollect;

            var ev = new PlantPollinatedEvent(uid, pollenToCollect);
            RaiseLocalEvent(bee.TargetFlower.Value, ref ev);
        }

        if (bee.PollenCarried >= bee.MaxPollenCarry)
        {
            bee.PollenCarried = bee.MaxPollenCarry;
            SetState(uid, bee, BeeState.ReturningToHive);
        }
        else
        {
            SetState(uid, bee, BeeState.SearchingFlower);
        }
    }

    private void TickReturning(EntityUid uid, BeeComponent bee, TransformComponent xform, float frameTime)
    {
        if (bee.HiveOwner == null || !EntityManager.EntityExists(bee.HiveOwner.Value))
        {
            SetState(uid, bee, BeeState.Idle);
            return;
        }

        var hivePos = _transform.GetWorldPosition(bee.HiveOwner.Value);
        var myPos = _transform.GetWorldPosition(uid);
        var dist = (hivePos - myPos).Length();

        if (dist < bee.ArrivalThreshold)
        {
            SetState(uid, bee, BeeState.DepositingPollen);
            return;
        }

        var direction = (hivePos - myPos).Normalized();
        var speed = 3f;
        var movement = direction * speed * frameTime;
        _transform.SetWorldPosition(uid, myPos + movement);
    }

    private void TickDepositing(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (bee.HiveOwner == null || !EntityManager.EntityExists(bee.HiveOwner.Value))
        {
            SetState(uid, bee, BeeState.Idle);
            return;
        }

        // Проверяем, что пчела всё ещё рядом с ульем
        var hivePos = _transform.GetWorldPosition(bee.HiveOwner.Value);
        var myPos = _transform.GetWorldPosition(uid);
        var dist = (hivePos - myPos).Length();

        if (dist > bee.ArrivalThreshold * 2f)
        {
            SetState(uid, bee, BeeState.ReturningToHive);
            return;
        }

        // Если уже заняты — не запускаем новый DoAfter
        if (bee.IsBusy)
            return;

        // Запускаем DoAfter и блокируем движение
        bee.IsBusy = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(1.5f), new BeeDepositingDoAfterEvent(), bee.HiveOwner.Value)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            EventTarget = uid,
            BlockDuplicate = true,
        };

        // Если не удалось запустить DoAfter — снимаем занятость
        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            bee.IsBusy = false;
            SetState(uid, bee, BeeState.ReturningToHive);
        }
    }

    private void OnDepositingDoAfter(EntityUid uid, BeeComponent bee, BeeDepositingDoAfterEvent args)
    {
        // В любом случае снимаем занятость — DoAfter завершился
        bee.IsBusy = false;

        // Проверяем, был ли DoAfter отменён
        if (args.DoAfter is { } doAfter && doAfter.CancelledTime != null)
        {
            SetState(uid, bee, BeeState.ReturningToHive);
            return;
        }

        if (bee.HiveOwner == null || !EntityManager.EntityExists(bee.HiveOwner.Value))
        {
            SetState(uid, bee, BeeState.Idle);
            return;
        }

        // Сдаём пыльцу в улей
        var ev = new BeePollenDepositedEvent(uid, bee.PollenCarried);
        RaiseLocalEvent(bee.HiveOwner.Value, ref ev);

        bee.PollenCarried = 0f;
        bee.TargetFlower = null;
        SetState(uid, bee, BeeState.Idle);
    }
}