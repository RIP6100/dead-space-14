// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeadSpace.Research.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ResearchDataDiskComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<ProtoId<ResearchFieldPrototype>, int> Data = new();

    [DataField, AutoNetworkedField]
    public int MaxFields = 2;
}
