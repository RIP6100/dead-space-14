// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.DeadSpace.Research.Components;

[RegisterComponent]
public sealed partial class ResearchDataSourceComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ResearchFieldPrototype> Field;

    [DataField]
    public int AmountPerSecond = 1;

    [DataField]
    public bool Active = true;
}
