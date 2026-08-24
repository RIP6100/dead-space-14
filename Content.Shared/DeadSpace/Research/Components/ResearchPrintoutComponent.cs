// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Research.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchPrintoutComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<TechnologyPrototype> Tech;

    [DataField, AutoNetworkedField]
    public Dictionary<Vector2i, ProtoId<ResearchFieldPrototype>> Placement = new();

    [DataField, AutoNetworkedField]
    public bool Fixed;
}
