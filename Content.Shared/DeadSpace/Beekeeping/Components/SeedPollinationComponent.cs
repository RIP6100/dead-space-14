// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.DeadSpace.Beekeeping;

/// <summary>
/// Компонент для семян. При посадке в гидропонику параметры копируются на лоток как PollinationComponent.
/// </summary>
[RegisterComponent]
public sealed partial class SeedPollinationComponent : Component
{
    [DataField]
    public bool IsFlowering = true;

    [DataField]
    public float PollenYield = 10f;

    [DataField]
    public float GrowthSpeedBonus = 0.3f;

    [DataField]
    public float BoostDuration = 300f;

    [DataField]
    public float PollinationCooldown = 60f;

    [DataField]
    public int InstantAgeBonus = 0;

    [DataField]
    public string PollinationSound = "/Audio/Effects/Fluids/splat.ogg";

    /// <summary>
    /// На сколько повышается потенция плодов на время действия опыления (0.2 = +20%).
    /// 0 = не влиять на потенцию.
    /// </summary>
    [DataField]
    public float PotencyBonus = 0.2f;
}