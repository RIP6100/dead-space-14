using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Beekeeping;

[Serializable, NetSerializable]
public sealed partial class BeePollinatingDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => new BeePollinatingDoAfterEvent();
}