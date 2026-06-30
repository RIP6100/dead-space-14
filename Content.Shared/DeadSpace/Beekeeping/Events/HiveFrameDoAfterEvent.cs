using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.DeadSpace.Beekeeping;

[Serializable, NetSerializable]
public sealed partial class HiveFrameDoAfterEvent : DoAfterEvent
{
    public NetEntity FrameNetEntity;
    public NetEntity UserNetEntity;
    
    public override DoAfterEvent Clone() => new HiveFrameDoAfterEvent 
    { 
        FrameNetEntity = FrameNetEntity,
        UserNetEntity = UserNetEntity
    };
}