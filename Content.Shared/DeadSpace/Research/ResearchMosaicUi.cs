// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Research;

[NetSerializable, Serializable]
public enum ResearchMosaicUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class MosaicSubmitMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class MosaicPlaceMessage : BoundUserInterfaceMessage
{
    public Vector2i Cell;
    public ProtoId<ResearchFieldPrototype> Field;

    public MosaicPlaceMessage(Vector2i cell, ProtoId<ResearchFieldPrototype> field)
    {
        Cell = cell;
        Field = field;
    }
}

[Serializable, NetSerializable]
public sealed class MosaicRemoveMessage : BoundUserInterfaceMessage
{
    public Vector2i Cell;

    public MosaicRemoveMessage(Vector2i cell)
    {
        Cell = cell;
    }
}

[Serializable, NetSerializable]
public sealed class MosaicCombineMessage : BoundUserInterfaceMessage
{
    public ProtoId<ResearchFieldPrototype> A;
    public ProtoId<ResearchFieldPrototype> B;

    public MosaicCombineMessage(ProtoId<ResearchFieldPrototype> a, ProtoId<ResearchFieldPrototype> b)
    {
        A = a;
        B = b;
    }
}

[Serializable, NetSerializable]
public sealed class ResearchMosaicBuiState : BoundUserInterfaceState
{
    public Dictionary<ProtoId<ResearchFieldPrototype>, int> Data;

    public ProtoId<TechnologyPrototype>? CurrentTech;

    public Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>>? Placement;

    public bool Fixed;

    public string? Status;

    public bool StatusIsError;

    public ResearchMosaicBuiState(
        Dictionary<ProtoId<ResearchFieldPrototype>, int> data,
        ProtoId<TechnologyPrototype>? currentTech,
        Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>>? placement,
        bool isFixed,
        string? status = null,
        bool statusIsError = false)
    {
        Data = data;
        CurrentTech = currentTech;
        Placement = placement;
        Fixed = isFixed;
        Status = status;
        StatusIsError = statusIsError;
    }
}
