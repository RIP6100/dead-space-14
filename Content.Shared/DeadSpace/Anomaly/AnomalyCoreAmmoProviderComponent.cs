// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.DeadSpace.Weapons.Anomaly.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.DeadSpace.Weapons.Anomaly.Components;

/// <summary>
/// Ammo provider for weapons powered by an anomaly core.
/// The core is inserted into an item slot and acts as a battery: every shot consumes charges,
/// and both the projectile that gets spawned AND how many charges it costs depend on which
/// type of core is currently loaded (see <see cref="AnomalyCoreAmmoEntry"/>).
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedAnomalyWeaponSystem))]
public sealed partial class AnomalyCoreAmmoProviderComponent : AmmoProviderComponent
{
    /// <summary>
    /// The id of the ItemSlots slot that holds the core.
    /// </summary>
    [DataField]
    public string CoreSlotId = "anomaly_core_slot";

    /// <summary>
    /// Maps an anomaly core prototype id to the projectile that should be fired, and to the
    /// amount of charges that shot costs. Stronger shots should have a higher <see cref="AnomalyCoreAmmoEntry.EnergyCost"/>
    /// so they burn through the core faster than weaker ones.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<EntProtoId, AnomalyCoreAmmoEntry> CoreToProjectile = new();

    /// <summary>
    /// Entry used if the loaded core has no explicit mapping.
    /// Leave null to refuse firing with unmapped cores.
    /// </summary>
    [DataField]
    public AnomalyCoreAmmoEntry? FallbackEntry;

    [DataField]
    public SoundSpecifier? NoCoreSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    [DataField]
    public LocId NoCoreMessage = "anomaly-core-weapon-no-core";

    [DataField]
    public LocId CoreDepletedMessage = "anomaly-core-weapon-core-depleted";

    [DataField]
    public LocId UnsupportedCoreMessage = "anomaly-core-weapon-unsupported-core";
}

/// <summary>
/// Describes what projectile a given anomaly core fires, and how many charges it costs to fire it.
/// </summary>
[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class AnomalyCoreAmmoEntry
{
    /// <summary>
    /// The projectile prototype to spawn.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Proto = default!;

    /// <summary>
    /// How many charges a single shot of this projectile consumes from the loaded core.
    /// Give more powerful/dangerous shots a higher cost so they drain the core faster.
    /// </summary>
    [DataField]
    public int EnergyCost = 1;
}
