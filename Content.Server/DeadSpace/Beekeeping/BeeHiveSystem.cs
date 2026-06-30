// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Beekeeping;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Storage;
using Content.Server.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class BeeHiveSystem : EntitySystem
{
    [Dependency] private readonly EntityManager _entities = default!;
    [Dependency] private readonly HiveFrameSystem _hiveFrame = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeeHiveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BeeHiveComponent, BeePollenDepositedEvent>(OnPollenDeposited);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BeeHiveComponent>();
        while (query.MoveNext(out var uid, out var hive))
        {
            if (!hive.Active) continue;

            // Спавн пчёл
            hive.SpawnTimer += frameTime;
            if (hive.SpawnTimer >= hive.SpawnInterval && hive.BeeCount < hive.MaxBees)
            {
                hive.SpawnTimer = 0f;
                SpawnBee(uid, hive);
            }

            // Тик производства
            hive.ProductionTimer += frameTime;
            if (hive.ProductionTimer >= hive.ProductionInterval)
            {
                hive.ProductionTimer = 0f;
                TryProduce(uid, hive);
            }
        }
    }

    private void SpawnBee(EntityUid uid, BeeHiveComponent hive)
    {
        var bee = _entities.SpawnEntity("MobBee", Transform(uid).Coordinates);
        if (TryComp<BeeComponent>(bee, out var beeComp))
        {
            beeComp.HiveOwner = uid;
        }
        hive.BeeCount++;
    }

    private void OnPollenDeposited(EntityUid uid, BeeHiveComponent hive,
                                   ref BeePollenDepositedEvent ev)
    {
        hive.PollenStored = MathF.Min(
            hive.PollenStored + ev.Amount,
            hive.MaxPollen);
    }

    private void TryProduce(EntityUid uid, BeeHiveComponent hive)
    {
        if (hive.PollenStored < hive.PollenPerHoney) return;

        if (!TryComp<StorageComponent>(uid, out var storage)) return;

        HiveFrameComponent? targetFrame = null;
        EntityUid? targetFrameUid = null;

        foreach (var item in storage.StoredItems.Keys)
        {
            if (!TryComp<HiveFrameComponent>(item, out var frame)) continue;
            if (frame.HoneycombAmount < frame.MaxCapacity)
            {
                targetFrame = frame;
                targetFrameUid = item;
                break;
            }
        }

        if (targetFrame == null || targetFrameUid == null) return;

        var honeyUnits = MathF.Floor(hive.PollenStored / hive.PollenPerHoney);
        var space      = targetFrame.MaxCapacity - targetFrame.HoneycombAmount;
        var produced   = MathF.Min(honeyUnits, space);

        if (produced < 1f) return;

        targetFrame.HoneycombAmount += produced;
        hive.PollenStored           -= produced * hive.PollenPerHoney;

        // Обновляем визуал рамки
        _hiveFrame.UpdateVisuals(targetFrameUid.Value, targetFrame);
    }

    private void OnExamined(EntityUid uid, BeeHiveComponent hive, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("beehive-examine-pollen",
            ("current", (int) hive.PollenStored),
            ("max",     (int) hive.MaxPollen)));

        args.PushMarkup(hive.Active
            ? Loc.GetString("beehive-examine-active")
            : Loc.GetString("beehive-examine-inactive"));
    }
}