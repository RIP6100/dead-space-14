// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class BeeHiveSystem : EntitySystem
{
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
            if (!hive.Active)
                continue;

            // Спавн пчёл.
            hive.SpawnTimer += frameTime;
            if (hive.SpawnTimer >= hive.SpawnInterval && hive.BeeCount < hive.MaxBees)
            {
                hive.SpawnTimer = 0f;
                SpawnBee(uid, hive);
            }

            // Тик производства мёда.
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
        var bee = Spawn("MobBee", Transform(uid).Coordinates);
        var beeComp = EnsureComp<BeeComponent>(bee);
        beeComp.HiveOwner = uid;
        hive.BeeCount++;
    }

    private void OnPollenDeposited(EntityUid uid, BeeHiveComponent hive, ref BeePollenDepositedEvent ev)
    {
        hive.PollenStored = MathF.Min(hive.PollenStored + ev.Amount, hive.MaxPollen);
    }

    private void TryProduce(EntityUid uid, BeeHiveComponent hive)
    {
        if (hive.PollenStored < hive.PollenPerHoney)
            return;

        if (!TryComp<StorageComponent>(uid, out var storage))
            return;

        HiveFrameComponent? targetFrame = null;
        EntityUid targetFrameUid = default;

        foreach (var item in storage.StoredItems.Keys)
        {
            if (!TryComp<HiveFrameComponent>(item, out var frame))
                continue;

            if (frame.HoneycombAmount < frame.MaxCapacity)
            {
                targetFrame = frame;
                targetFrameUid = item;
                break;
            }
        }

        if (targetFrame == null)
            return;

        var honeyUnits = MathF.Floor(hive.PollenStored / hive.PollenPerHoney);
        var space = targetFrame.MaxCapacity - targetFrame.HoneycombAmount;
        var produced = MathF.Min(honeyUnits, space);

        if (produced < 1f)
            return;

        targetFrame.HoneycombAmount += produced;
        hive.PollenStored -= produced * hive.PollenPerHoney;

        _hiveFrame.UpdateVisuals(targetFrameUid, targetFrame);
    }

    private void OnExamined(EntityUid uid, BeeHiveComponent hive, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("beehive-examine-pollen",
            ("current", (int) hive.PollenStored),
            ("max", (int) hive.MaxPollen)));

        args.PushMarkup(hive.Active
            ? Loc.GetString("beehive-examine-active")
            : Loc.GetString("beehive-examine-inactive"));
    }
}