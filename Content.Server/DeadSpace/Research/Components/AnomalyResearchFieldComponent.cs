// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Research.Components;

[RegisterComponent]
public sealed partial class AnomalyResearchFieldComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<ResearchFieldPrototype>, float> Fields = new();

    [ViewVariables]
    public Dictionary<ProtoId<ResearchFieldPrototype>, float> Accumulator = new();
}
