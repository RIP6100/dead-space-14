// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Server.DeadSpace.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DeadSpace.Research;
using Content.Shared.DeadSpace.Research.Components;
using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.DeadSpace.Research;

public sealed class ResearchMosaicConsoleSystem : EntitySystem
{
    [Dependency] private readonly ResearchSystem _research = default!;
    [Dependency] private readonly ResearchDataSystem _data = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedResearchMosaicSystem _mosaic = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string PrintoutSlot = "printout_slot";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, MosaicSubmitMessage>(OnSubmit);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, MosaicPlaceMessage>(OnPlace);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, MosaicRemoveMessage>(OnRemove);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, MosaicCombineMessage>(OnCombine);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, BeforeActivatableUIOpenEvent>(OnBeforeUiOpen);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ResearchMosaicConsoleComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnPlace(EntityUid uid, ResearchMosaicConsoleComponent component, MosaicPlaceMessage args)
    {
        if (!TryGetPrintout(uid, out var printout, out var pc) || pc.Fixed || string.IsNullOrEmpty(pc.Tech.Id))
            return;
        if (!_proto.TryIndex(pc.Tech, out var tech))
            return;
        if (!_proto.HasIndex(args.Field))
            return;

        if (tech.MosaicBoard is not { } board)
            return;
        if (!IsPlaceableCell(board, args.Cell) || pc.Placement.ContainsKey(args.Cell))
            return;

        if (!_research.TryGetClientServer(uid, out var server, out _))
            return;

        if (!_data.TrySpend(server.Value, args.Field, 1))
        {
            UpdateUiState(uid, Loc.GetString("research-mosaic-place-nodata", ("field", Loc.GetString(_proto.Index(args.Field).Name))), true);
            return;
        }

        pc.Placement[args.Cell] = args.Field;
        Dirty(printout, pc);
        UpdateUiState(uid);
    }

    private void OnRemove(EntityUid uid, ResearchMosaicConsoleComponent component, MosaicRemoveMessage args)
    {
        if (!TryGetPrintout(uid, out var printout, out var pc) || pc.Fixed)
            return;

        if (!pc.Placement.TryGetValue(args.Cell, out var field))
            return;

        pc.Placement.Remove(args.Cell);
        Dirty(printout, pc);

        string? status = null;
        if (_research.TryGetClientServer(uid, out var server, out _)
            && HasRefundUpgrade(server.Value)
            && _random.Prob(0.5f))
        {
            _data.AddData(server.Value, field, 1);
            status = Loc.GetString("research-mosaic-refund", ("field", Loc.GetString(_proto.Index(field).Name)));
        }

        UpdateUiState(uid, status);
    }

    private bool HasRefundUpgrade(EntityUid server)
    {
        if (!TryComp<TechnologyDatabaseComponent>(server, out var db))
            return false;

        foreach (var techId in db.UnlockedTechnologies)
        {
            if (_proto.TryIndex<TechnologyPrototype>(techId, out var tech) && tech.GrantsMosaicRefund)
                return true;
        }
        return false;
    }

    private void OnCombine(EntityUid uid, ResearchMosaicConsoleComponent component, MosaicCombineMessage args)
    {
        if (!_research.TryGetClientServer(uid, out var server, out _))
            return;

        if (!_proto.HasIndex(args.A) || !_proto.HasIndex(args.B))
            return;

        if (args.A == args.B)
            return;

        var pool = _data.GetAvailable(server.Value);
        if (pool.GetValueOrDefault(args.A) < 1 || pool.GetValueOrDefault(args.B) < 1)
        {
            UpdateUiState(uid, Loc.GetString("research-mosaic-draft-nodata"), true);
            return;
        }

        var success = _mosaic.TryGetCombination(args.A, args.B, out var result);

        _data.TrySpend(server.Value, args.A, 1);
        _data.TrySpend(server.Value, args.B, 1);

        string status;
        var error = false;
        if (success)
        {
            if (_data.AddData(server.Value, result, 1))
            {
                _audio.PlayPvs(component.UnlockSound, uid);
                status = Loc.GetString("research-mosaic-draft-success", ("field", Loc.GetString(_proto.Index(result).Name)));
            }
            else
            {
                _data.AddData(server.Value, args.A, 1);
                _data.AddData(server.Value, args.B, 1);
                status = Loc.GetString("research-mosaic-draft-noroom");
                error = true;
            }
        }
        else
        {
            status = Loc.GetString("research-mosaic-draft-fail");
            error = true;
        }

        UpdateUiState(uid, status, error);
    }

    private void OnSubmit(EntityUid uid, ResearchMosaicConsoleComponent component, MosaicSubmitMessage args)
    {
        if (!_research.TryGetClientServer(uid, out var server, out _))
            return;

        if (!TryGetPrintout(uid, out var printout, out var pc) || string.IsNullOrEmpty(pc.Tech.Id))
            return;

        if (pc.Fixed)
            return;

        var result = _data.TryUnlockTechnology(server.Value, pc.Tech, pc.Placement);
        string status;
        var error = false;
        if (result == MosaicUnlockResult.Success)
        {
            pc.Fixed = true;
            Dirty(printout, pc);
            _audio.PlayPvs(component.UnlockSound, uid);
            status = Loc.GetString("research-mosaic-success");
        }
        else
        {
            status = Loc.GetString(FailReasonLoc(result));
            error = true;
        }

        UpdateUiState(uid, status, error);
    }

    private static string FailReasonLoc(MosaicUnlockResult result) => result switch
    {
        MosaicUnlockResult.NoTech => "research-mosaic-fail-notech",
        MosaicUnlockResult.NotAvailable => "research-mosaic-fail-notavailable",
        MosaicUnlockResult.NotEnoughData => "research-mosaic-fail-data",
        _ => "research-mosaic-fail-invalid",
    };

    private static bool IsPlaceableCell(MosaicBoard board, Vector2i cell)
    {
        var s = -cell.X - cell.Y;
        if (Math.Abs(cell.X) > board.Radius || Math.Abs(cell.Y) > board.Radius || Math.Abs(s) > board.Radius)
            return false;
        if (board.Holes.Contains(cell))
            return false;
        foreach (var endpoint in board.Endpoints)
        {
            if (endpoint.Pos == cell)
                return false;
        }
        return true;
    }

    private void UpdateUiState(EntityUid console, string? status = null, bool statusIsError = false)
    {
        if (!_research.TryGetClientServer(console, out var server, out _))
            return;

        var data = _data.GetAvailable(server.Value);

        ProtoId<TechnologyPrototype>? currentTech = null;
        Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>>? placement = null;
        var isFixed = false;
        if (TryGetPrintout(console, out _, out var pc) && !string.IsNullOrEmpty(pc.Tech.Id))
        {
            currentTech = pc.Tech;
            placement = pc.Placement;
            isFixed = pc.Fixed;
        }

        var state = new ResearchMosaicBuiState(data, currentTech, placement, isFixed, status, statusIsError);
        _ui.SetUiState(console, ResearchMosaicUiKey.Key, state);
    }

    private bool TryGetPrintout(EntityUid console, out EntityUid printout, out ResearchPrintoutComponent comp)
    {
        printout = default;
        comp = default!;

        if (!_itemSlots.TryGetSlot(console, PrintoutSlot, out var slot) ||
            slot.Item is not { } item ||
            !TryComp<ResearchPrintoutComponent>(item, out var c))
        {
            return false;
        }

        printout = item;
        comp = c;
        return true;
    }

    private void OnBeforeUiOpen(EntityUid uid, ResearchMosaicConsoleComponent component, BeforeActivatableUIOpenEvent args)
    {
        UpdateUiState(uid);
    }

    private void OnInserted(EntityUid uid, ResearchMosaicConsoleComponent component, EntInsertedIntoContainerMessage args)
    {
        UpdateUiState(uid);
    }

    private void OnRemoved(EntityUid uid, ResearchMosaicConsoleComponent component, EntRemovedFromContainerMessage args)
    {
        UpdateUiState(uid);
    }
}
