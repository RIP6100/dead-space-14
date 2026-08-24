// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Research.Prototypes;

[Prototype]
public sealed partial class ResearchFieldPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField]
    public LocId Description = string.Empty;

    [DataField(required: true)]
    public Color Color;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField]
    public SpriteSpecifier? HexSprite;

    [DataField]
    public List<ProtoId<ResearchFieldPrototype>> Components = new();

    public bool IsBase => Components.Count == 0;
}
