// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT


using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Beekeeping;

[Serializable, NetSerializable]
public enum HiveFrameVisuals : byte
{
    HasHoneycomb,
}

[Serializable, NetSerializable]
public enum HiveFrameVisualLayers : byte
{
    Base,
}

[RegisterComponent]
public sealed partial class HiveFrameComponent : Component
{
    /// <summary>
    /// Сколько сот накоплено в рамке.
    /// </summary>
    [DataField]
    public float HoneycombAmount = 0f;

    /// <summary>
    /// Максимум сот в одной рамке.
    /// </summary>
    [DataField]
    public float MaxCapacity = 50f;
}