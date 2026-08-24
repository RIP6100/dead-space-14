// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Research.Components;
using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.Prototypes;
using Content.Server.DeadSpace.Research.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Research.Components;
using Content.Server.Anomaly;
using Content.Server.Anomaly.Components;
using Content.Shared.DeadSpace.Research;
using Content.Server.Research.Systems;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Maths;

namespace Content.Server.DeadSpace.Research;

public enum MosaicUnlockResult : byte
{
    Success,
    NoTech,
    NotAvailable,
    InvalidMosaic,
    NotEnoughData,
}

public sealed class ResearchDataSystem : EntitySystem
{
    [Dependency] private readonly AnomalySystem _anomaly = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedResearchMosaicSystem _mosaic = default!;
    [Dependency] private readonly ResearchSystem _research = default!;

    private float _accumulator;
    public override void Update(float frameTime)
    {
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<ResearchDataStorageComponent, ResearchServerComponent>();
        while (query.MoveNext(out var uid, out _, out var server))
        {
            if (!this.IsPowered(uid, EntityManager))
                continue;

            var ev = new ResearchServerGetDataPerSecondEvent(uid, new Dictionary<ProtoId<ResearchFieldPrototype>, int>());
            foreach (var client in server.Clients)
            {
                RaiseLocalEvent(client, ref ev);
            }

            foreach (var (field, amount) in ev.Data)
            {
                AddData(uid, field, amount);
            }
        }
    }
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AnomalyVesselComponent, ResearchServerGetDataPerSecondEvent>(OnVesselGetData);
        SubscribeLocalEvent<ResearchDataSourceComponent, ResearchServerGetDataPerSecondEvent>(OnGetDataPerSecond);
    }
    public IEnumerable<Entity<ResearchDataDiskComponent>> GetDisks(EntityUid server)
    {
        if (!TryComp<ItemSlotsComponent>(server, out var slots))
            yield break;

        foreach (var slot in slots.Slots.Values)
        {
            if (slot.Item is { } item && TryComp<ResearchDataDiskComponent>(item, out var comp))
                yield return (item, comp);
        }
    }

    public Dictionary<ProtoId<ResearchFieldPrototype>, int> GetAvailable(EntityUid server)
    {
        var result = new Dictionary<ProtoId<ResearchFieldPrototype>, int>();
        foreach (var disk in GetDisks(server))
        {
            foreach (var (field, amount) in disk.Comp.Data)
                result[field] = result.GetValueOrDefault(field) + amount;
        }
        return result;
    }

    public bool AddData(EntityUid server, ProtoId<ResearchFieldPrototype> field, int amount)
    {
        if (amount <= 0)
            return false;

        Entity<ResearchDataDiskComponent>? target = null;
        foreach (var disk in GetDisks(server))
        {
            if (disk.Comp.Data.ContainsKey(field))
            {
                target = disk;
                break;
            }

            if (target == null && disk.Comp.Data.Count < disk.Comp.MaxFields)
                target = disk;
        }

        if (target is not { } t)
            return false;

        t.Comp.Data[field] = t.Comp.Data.GetValueOrDefault(field) + amount;
        Dirty(t);
        return true;
    }

    private void SpendField(EntityUid server, ProtoId<ResearchFieldPrototype> field, int amount)
    {
        foreach (var disk in GetDisks(server))
        {
            if (amount <= 0)
                break;

            if (!disk.Comp.Data.TryGetValue(field, out var have) || have <= 0)
                continue;

            var take = Math.Min(have, amount);
            var left = have - take;
            if (left > 0)
                disk.Comp.Data[field] = left;
            else
                disk.Comp.Data.Remove(field);

            amount -= take;
            Dirty(disk);
        }
    }
    private void OnGetDataPerSecond(Entity<ResearchDataSourceComponent> source, ref ResearchServerGetDataPerSecondEvent args)
    {
        if (!source.Comp.Active)
            return;

        if (!this.IsPowered(source, EntityManager))
            return;

        args.Data[source.Comp.Field] = args.Data.GetValueOrDefault(source.Comp.Field) + source.Comp.AmountPerSecond;
    }
    private void OnVesselGetData(Entity<AnomalyVesselComponent> vessel, ref ResearchServerGetDataPerSecondEvent args)
    {
        if (!this.IsPowered(vessel, EntityManager))
            return;

        if (vessel.Comp.Anomaly is not { } anomaly)
            return;

        if (!TryComp<AnomalyResearchFieldComponent>(anomaly, out var fieldComp))
            return;

        var strength = _anomaly.GetAnomalyStrength(anomaly);
        foreach (var (field, rate) in fieldComp.Fields)
        {
            var acc = fieldComp.Accumulator.GetValueOrDefault(field) + rate * vessel.Comp.PointMultiplier * strength;
            var whole = (int) MathF.Floor(acc);
            if (whole > 0)
                args.Data[field] = args.Data.GetValueOrDefault(field) + whole;
            fieldComp.Accumulator[field] = acc - whole;
        }
    }

    public MosaicUnlockResult TryUnlockTechnology(
        EntityUid server,
        ProtoId<TechnologyPrototype> techId,
        Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> playerPlacement)
    {
        if (string.IsNullOrEmpty(techId.Id) || !_proto.TryIndex(techId, out var tech))
            return MosaicUnlockResult.NoTech;

        if (!TryComp<TechnologyDatabaseComponent>(server, out var db) || !_mosaic.IsAvailable(db, tech))
            return MosaicUnlockResult.NotAvailable;

        if (tech.MosaicBoard is not { } board)
            return MosaicUnlockResult.InvalidMosaic;

        var full = new Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>>(playerPlacement);
        foreach (var endpoint in board.Endpoints)
            full[endpoint.Pos] = endpoint.Field;

        if (!IsPlacementValid(board, full))
            return MosaicUnlockResult.InvalidMosaic;

        _research.AddTechnology(server, tech);
        return MosaicUnlockResult.Success;
    }

    public bool TrySpend(EntityUid server, ProtoId<ResearchFieldPrototype> field, int amount)
    {
        if (GetAvailable(server).GetValueOrDefault(field) < amount)
            return false;

        SpendField(server, field, amount);
        return true;
    }

    private bool IsPlacementValid(MosaicBoard board, Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> placement)
    {
        if (placement.Count == 0 || board.Endpoints.Count == 0)
            return false;

        var holes = new HashSet<Vector2i>(board.Holes);
        var cells = new HashSet<Vector2i>();
        var radius = board.Radius;
        for (var q = -radius; q <= radius; q++)
        {
            for (var r = -radius; r <= radius; r++)
            {
                var s = -q - r;
                if (Math.Abs(q) <= radius && Math.Abs(r) <= radius && Math.Abs(s) <= radius)
                {
                    var cell = new Vector2i(q, r);
                    if (!holes.Contains(cell))
                        cells.Add(cell);
                }
            }
        }

        foreach (var pos in placement.Keys)
        {
            if (!cells.Contains(pos))
                return false;
        }

        foreach (var endpoint in board.Endpoints)
        {
            if (!placement.TryGetValue(endpoint.Pos, out var f) || f != endpoint.Field)
                return false;
        }

        var start = board.Endpoints[0].Pos;
        var visited = new HashSet<Vector2i> { start };
        var queue = new Queue<Vector2i>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            var curField = placement[cur];
            foreach (var n in Neighbors(cur))
            {
                if (visited.Contains(n) || !placement.TryGetValue(n, out var nField))
                    continue;
                if (!_mosaic.AreFieldsRelated(curField, nField))
                    continue;

                visited.Add(n);
                queue.Enqueue(n);
            }
        }

        foreach (var endpoint in board.Endpoints)
        {
            if (!visited.Contains(endpoint.Pos))
                return false;
        }

        return true;
    }

    private static Vector2i[] Neighbors(Vector2i c)
    {
        return new[]
        {
            new Vector2i(c.X + 1, c.Y), new Vector2i(c.X - 1, c.Y),
            new Vector2i(c.X, c.Y + 1), new Vector2i(c.X, c.Y - 1),
            new Vector2i(c.X + 1, c.Y - 1), new Vector2i(c.X - 1, c.Y + 1),
        };
    }
}
