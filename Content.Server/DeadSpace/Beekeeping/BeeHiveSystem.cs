// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Beekeeping;
using Content.Shared.Examine;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Server.DeadSpace.Beekeeping;

public sealed class BeeHiveSystem : EntitySystem
{
    [Dependency] private readonly HiveFrameSystem _hiveFrame = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly BeeAISystem _beeAI = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BeeHiveComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BeeHiveComponent, BeePollenDepositedEvent>(OnPollenDeposited);
        SubscribeLocalEvent<BeeHiveComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<BeeHiveComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    /// <summary>
    /// Улей активен только когда в слоте матки находится именно матка.
    /// ЕДИНАЯ точка правды об активности улья - используется и здесь, и в BeeAISystem
    /// (через зависимость), чтобы условие активности не дублировалось.
    /// </summary>
    public bool IsActive(EntityUid uid, BeeHiveComponent? hive = null)
    {
        if (!Resolve(uid, ref hive, false))
            return false;

        var queen = _itemSlots.GetItemOrNull(uid, hive.QueenSlotId);
        return queen != null && HasComp<QueenBeeComponent>(queen.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BeeHiveComponent>();
        while (query.MoveNext(out var uid, out var hive))
        {
            if (!IsActive(uid, hive))
                continue;

            // Спавн пчёл. Число живых пчёл считаем запросом, а не храним в поле -
            // так счётчик не может рассинхронизироваться с реальностью.
            hive.SpawnTimer += frameTime;
            if (hive.SpawnTimer >= hive.SpawnInterval && CountBees(uid) < hive.MaxBees)
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

    /// <summary>
    /// Когда матку вставили/вынули - командуем всем пчёлам этого улья вернуться домой.
    /// (Если матки нет - они долетят до улья и встанут в Idle; спавн новых прекращается.)
    /// </summary>
    private void OnContainerChanged(EntityUid uid, BeeHiveComponent hive, ContainerModifiedMessage args)
    {
        if (args.Container.ID != hive.QueenSlotId)
            return;

        // Матку вынули - отзываем пчёл домой.
        if (!IsActive(uid, hive))
            RecallBees(uid);
    }

    /// <summary>
    /// Приказывает всем пчёлам этого улья вернуться домой (через BeeAISystem,
    /// без прямого изменения их состояния - логика перехода живёт в BeeAISystem).
    /// </summary>
    private void RecallBees(EntityUid hiveUid)
    {
        var query = EntityQueryEnumerator<BeeComponent>();
        while (query.MoveNext(out var beeUid, out var bee))
        {
            if (bee.HiveOwner != hiveUid)
                continue;

            _beeAI.RecallToHive(beeUid, bee);
        }
    }

    private void SpawnBee(EntityUid uid, BeeHiveComponent hive)
    {
        var bee = Spawn("MobBee", Transform(uid).Coordinates);
        var beeComp = EnsureComp<BeeComponent>(bee);
        beeComp.HiveOwner = uid;
    }

    /// <summary>
    /// Считает количество живых пчёл, принадлежащих данному улью.
    /// Используется вместо хранимого счётчика, чтобы исключить рассинхрон.
    /// </summary>
    private int CountBees(EntityUid hiveUid)
    {
        var count = 0;
        var query = EntityQueryEnumerator<BeeComponent>();
        while (query.MoveNext(out _, out var bee))
        {
            if (bee.HiveOwner == hiveUid)
                count++;
        }

        return count;
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

        args.PushMarkup(IsActive(uid, hive)
            ? Loc.GetString("beehive-examine-active")
            : Loc.GetString("beehive-examine-inactive"));
    }
}