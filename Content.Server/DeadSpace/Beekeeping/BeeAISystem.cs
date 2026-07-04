// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.DoAfter;
using Content.Shared.NPC;
using Content.Shared.NPC.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class BeeAISystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly NPCSteeringSystem _npcSteering = default!;
    [Dependency] private readonly BeeHiveSystem _beeHive = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeeComponent, MapInitEvent>(OnBeeMapInit);
        SubscribeLocalEvent<BeeComponent, ComponentShutdown>(OnBeeShutdown);
        SubscribeLocalEvent<BeeComponent, BeePollinatingDoAfterEvent>(OnPollinatingDoAfter);
        SubscribeLocalEvent<BeeComponent, BeeDepositingDoAfterEvent>(OnDepositingDoAfter);
    }

    /// <summary>
    /// Настройка пчелы при появлении на карте:
    ///  1. Вешаем ActiveNPCComponent - без него NPCSteeringSystem не двигает сущность.
    ///  2. Снимаем унаследованный от SimpleSpaceMobBase компонент HTN.
    ///
    /// Почему RemComp, а не чистое yaml-наследование: движок RobustToolbox НЕ умеет
    /// "вычитать" компонент родителя в дочернем прототипе. MobBee наследует HTN от
    /// SimpleSpaceMobBase (ради здоровья/крови/атмосферы), и убрать только HTN в yaml,
    /// сохранив остальное, нельзя. Снятие в рантайме - штатный приём SS14 для таких
    /// случаев. Без этого встроенный ИИ (HTN) параллельно с нашим BeeAISystem дёргал бы
    /// NPCSteeringSystem, и две системы конфликтовали бы за управление движением
    /// (симптом: пчела "залипает" на месте - см. историю багов).
    ///
    /// MapInitEvent (а не ComponentStartup) - чтобы удаление гарантированно произошло
    /// после полной сборки сущности из прототипа, когда HTN уже добавлен.
    /// </summary>
    private void OnBeeMapInit(EntityUid uid, BeeComponent bee, MapInitEvent args)
    {
        EnsureComp<ActiveNPCComponent>(uid);
        RemComp<HTNComponent>(uid);
    }

    /// <summary>
    /// При удалении пчелы снимаем регистрацию в NPCSteeringSystem.
    /// (Счётчик пчёл улья больше не хранится - он вычисляется запросом в BeeHiveSystem,
    /// поэтому уменьшать здесь ничего не нужно.)
    /// </summary>
    private void OnBeeShutdown(EntityUid uid, BeeComponent bee, ComponentShutdown args)
    {
        _npcSteering.Unregister(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BeeComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bee, out var xform))
        {
            // Полностью блокируем обновление пчелы, если она занята DoAfter.
            if (bee.IsBusy)
                continue;

            bee.StateTimer += frameTime;

            switch (bee.State)
            {
                case BeeState.Idle: TickIdle(uid, bee); break;
                case BeeState.SearchingFlower: TickSearching(uid, bee, xform); break;
                case BeeState.MovingToFlower: TickMoving(uid, bee, xform); break;
                case BeeState.Pollinating: TickPollinating(uid, bee, xform); break;
                case BeeState.ReturningToHive: TickReturning(uid, bee, xform); break;
                case BeeState.DepositingPollen: TickDepositing(uid, bee, xform); break;
            }
        }
    }

    private void SetState(EntityUid uid, BeeComponent bee, BeeState newState)
    {
        if (bee.State == newState)
            return;

        // Уходим из состояния с DoAfter — отменяем DoAfter и снимаем занятость.
        if (bee.State is BeeState.Pollinating or BeeState.DepositingPollen)
            CancelActiveDoAfters(uid, bee);

        // Уходим из состояния движения — отписываем пчелу от NPCSteeringSystem,
        // чтобы она не продолжала тянуться к старой цели, пока стоит на месте.
        if (bee.State is BeeState.MovingToFlower or BeeState.ReturningToHive)
            _npcSteering.Unregister(uid);

        bee.State = newState;
        bee.StateTimer = 0f;
    }

    /// <summary>
    /// Публичный API: приказать пчеле немедленно вернуться к улью.
    /// Используется, например, BeeHiveSystem при извлечении матки, чтобы не менять
    /// состояние пчелы напрямую из чужой системы (переход идёт через SetState, который
    /// корректно отменяет DoAfter и снимает steering - логика перехода в одном месте).
    /// Пчёл, уже находящихся дома или на пути домой, не трогаем.
    /// </summary>
    public void RecallToHive(EntityUid uid, BeeComponent? bee = null)
    {
        if (!Resolve(uid, ref bee, false))
            return;

        if (bee.State is BeeState.Idle or BeeState.ReturningToHive or BeeState.DepositingPollen)
            return;

        bee.TargetFlower = null;
        SetState(uid, bee, BeeState.ReturningToHive);
    }

    /// <summary>
    /// Отменяет все активные DoAfter у пчелы и снимает флаг занятости.
    /// </summary>
    private void CancelActiveDoAfters(EntityUid uid, BeeComponent bee)
    {
        if (TryComp<DoAfterComponent>(uid, out var doAfterComp))
        {
            foreach (var doAfter in doAfterComp.DoAfters.Values)
            {
                if (!doAfter.Cancelled && !doAfter.Completed)
                    _doAfter.Cancel(doAfter.Id);
            }
        }

        bee.IsBusy = false;
    }

    /// <summary>
    /// Проверяет, что улей пчелы всё ещё существует. Если нет — переводит в Idle.
    /// </summary>
    private bool HasValidHive(EntityUid uid, BeeComponent bee)
    {
        if (bee.HiveOwner != null && Exists(bee.HiveOwner.Value))
            return true;

        SetState(uid, bee, BeeState.Idle);
        return false;
    }

    /// <summary>
    /// Проверяет, что цель-цветок пчелы всё ещё существует. Если нет — в SearchingFlower.
    /// </summary>
    private bool HasValidFlower(EntityUid uid, BeeComponent bee)
    {
        if (bee.TargetFlower != null && Exists(bee.TargetFlower.Value))
            return true;

        bee.TargetFlower = null;
        SetState(uid, bee, BeeState.SearchingFlower);
        return false;
    }

    private float DistanceTo(EntityUid target, TransformComponent xform)
    {
        var targetPos = _transform.GetWorldPosition(target);
        var myPos = _transform.GetWorldPosition(xform);
        return (targetPos - myPos).Length();
    }

    private void TickIdle(EntityUid uid, BeeComponent bee)
    {
        if (bee.StateTimer < bee.IdleCooldown)
            return;

        if (!HasValidHive(uid, bee))
            return;

        // Не вылетаем, пока улей не активен (в нём нет матки).
        if (!_beeHive.IsActive(bee.HiveOwner!.Value))
            return;

        SetState(uid, bee, BeeState.SearchingFlower);
    }

    private void TickSearching(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        var worldPos = _transform.GetWorldPosition(xform);
        EntityUid? best = null;
        var bestDist = float.MaxValue;

        var query = EntityQueryEnumerator<PollinationComponent, TransformComponent>();
        while (query.MoveNext(out var flowerUid, out var poll, out var flowerXform))
        {
            if (!poll.IsFlowering || poll.WasPollinated)
                continue;

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

    private void TickMoving(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (!HasValidFlower(uid, bee))
            return;

        // Пчела слишком долго не может добраться (например, цветок недостижим) - ищем другую цель.
        if (bee.StateTimer > bee.MaxMovingTime)
        {
            bee.TargetFlower = null;
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (DistanceTo(bee.TargetFlower!.Value, xform) < bee.ArrivalThreshold)
        {
            SetState(uid, bee, BeeState.Pollinating);
            return;
        }

        // Регистрируем/обновляем цель в NPCSteeringSystem - дальше он сам физически
        // двигает пчелу к цветку, огибая стены и другие препятствия.
        _npcSteering.TryRegister(uid, Transform(bee.TargetFlower.Value).Coordinates);
    }

    private void TickPollinating(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (!HasValidFlower(uid, bee))
            return;

        // Пчела отдалилась от цветка (например, её оттолкнули) — снова к поиску.
        if (DistanceTo(bee.TargetFlower!.Value, xform) > bee.ArrivalThreshold * 2f)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (!CanPollinate(bee.TargetFlower.Value))
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (bee.IsBusy)
            return;

        bee.IsBusy = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(bee.PollinatingDuration), new BeePollinatingDoAfterEvent(), bee.TargetFlower.Value)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            EventTarget = uid,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            bee.IsBusy = false;
            SetState(uid, bee, BeeState.SearchingFlower);
        }
    }

    private void OnPollinatingDoAfter(EntityUid uid, BeeComponent bee, BeePollinatingDoAfterEvent args)
    {
        bee.IsBusy = false;

        if (args.DoAfter is { } doAfter && doAfter.CancelledTime != null)
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        if (!HasValidFlower(uid, bee))
            return;

        if (!CanPollinate(bee.TargetFlower!.Value))
        {
            SetState(uid, bee, BeeState.SearchingFlower);
            return;
        }

        // Собираем пыльцу.
        TryComp<PollinationComponent>(bee.TargetFlower.Value, out var pollination);
        var pollenToCollect = Math.Min(pollination!.PollenYield, bee.MaxPollenCarry - bee.PollenCarried);

        if (pollenToCollect > 0f)
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

    private void TickReturning(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (!HasValidHive(uid, bee))
            return;

        if (DistanceTo(bee.HiveOwner!.Value, xform) < bee.ArrivalThreshold)
        {
            SetState(uid, bee, BeeState.DepositingPollen);
            return;
        }

        var targetCoords = Transform(bee.HiveOwner.Value).Coordinates;

        // Если steering потерял путь (NoPath) или пчела слишком долго висит - принудительно
        // перерегистрируем через Register (TryRegister не сработает: координаты улья не меняются,
        // а он выходит раньше, если Coordinates те же). Это перезапускает поиск пути.
        if (bee.StateTimer > bee.MaxMovingTime || SteeringLostPath(uid))
        {
            bee.StateTimer = 0f;
            _npcSteering.Unregister(uid);
            _npcSteering.Register(uid, targetCoords);
            return;
        }

        _npcSteering.TryRegister(uid, targetCoords);
    }

    /// <summary>
    /// Проверяет, потерял ли steering путь к цели (нужна перерегистрация).
    /// </summary>
    private bool SteeringLostPath(EntityUid uid)
    {
        return TryComp<NPCSteeringComponent>(uid, out var steering) &&
               steering.Status == SteeringStatus.NoPath;
    }

    private void TickDepositing(EntityUid uid, BeeComponent bee, TransformComponent xform)
    {
        if (!HasValidHive(uid, bee))
            return;

        // Пчела отдалилась от улья — возвращаемся.
        if (DistanceTo(bee.HiveOwner!.Value, xform) > bee.ArrivalThreshold * 2f)
        {
            SetState(uid, bee, BeeState.ReturningToHive);
            return;
        }

        if (bee.IsBusy)
            return;

        bee.IsBusy = true;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid, TimeSpan.FromSeconds(bee.DepositingDuration), new BeeDepositingDoAfterEvent(), bee.HiveOwner.Value)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            EventTarget = uid,
            BlockDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
        {
            bee.IsBusy = false;
            SetState(uid, bee, BeeState.ReturningToHive);
        }
    }

    private void OnDepositingDoAfter(EntityUid uid, BeeComponent bee, BeeDepositingDoAfterEvent args)
    {
        bee.IsBusy = false;

        if (args.DoAfter is { } doAfter && doAfter.CancelledTime != null)
        {
            SetState(uid, bee, BeeState.ReturningToHive);
            return;
        }

        if (!HasValidHive(uid, bee))
            return;

        // Сдаём пыльцу в улей.
        var ev = new BeePollenDepositedEvent(uid, bee.PollenCarried);
        RaiseLocalEvent(bee.HiveOwner!.Value, ref ev);

        bee.PollenCarried = 0f;
        bee.TargetFlower = null;
        SetState(uid, bee, BeeState.Idle);
    }

    /// <summary>
    /// Проверяет, можно ли опылить растение (цветёт и не в кулдауне).
    /// </summary>
    private bool CanPollinate(EntityUid flower)
    {
        if (!HasComp<PollinationComponent>(flower))
            return false;

        var ev = new CanPollinateEvent();
        RaiseLocalEvent(flower, ref ev);
        return ev.CanPollinate;
    }
}