using Content.Server.Atmos.EntitySystems;
﻿using Content.Shared.Atmos;

namespace Content.Server.Atmos.Components;

/// <summary>
/// This is basically a reverse scrubber but using <see cref="GetFilterAirEvent"/>.
/// </summary>
[RegisterComponent, Access(typeof(AirFilterSystem))]
public sealed partial class AirFilterComponent : Component
{
    // DS14: gas indices (see GasSetSerializer) so gases added purely in YAML can be filtered. YAML still lists gas names.
    /// <summary>
    /// Gases that will be filtered out of internal air
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(GasSetSerializer))]
    public HashSet<int> Gases = new();

    /// <summary>
    /// Gases that will be filtered out of internal air to maintain oxygen ratio.
    /// When oxygen is below <see cref="TargetOxygen"/>, these gases will be filtered instead of <see cref="Gases"/>.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(GasSetSerializer))]
    public HashSet<int> OverflowGases = new();

    /// <summary>
    /// Minimum oxygen fraction before it will start removing <see cref="OverflowGases"/>.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TargetOxygen = 0.21f;

    /// <summary>
    /// Gas to consider oxygen for <see cref="TargetOxygen"/> and <see cref="OverflowGases"/> logic.
    /// </summary>
    /// <remarks>
    /// For slime you might want to change this to be nitrogen, and overflowgases to remove oxygen.
    /// However theres still no real danger since standard atmos is mostly nitrogen so nitrogen tends to 100% anyway.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public Gas Oxygen = Gas.Oxygen;

    /// <summary>
    /// Fraction of target volume to transfer every second.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public float TransferRate = 0.1f;
}
