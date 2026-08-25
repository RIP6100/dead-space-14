using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

/// <summary>
/// Adjust the amount of Moles stored in this set of lungs based on a given dictionary of gasses and ratios.
/// The amount of gas adjusted is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class ModifyLungGasEntityEffectSystem : EntityEffectSystem<LungComponent, ModifyLungGas>
{
    [Dependency] private readonly SharedAtmosphereSystem _atmos = default!; // DS14: resolve gas prototype IDs to indices

    // TODO: This shouldn't be an entity effect, gasses should just metabolize and make a byproduct by default...
    protected override void Effect(Entity<LungComponent> entity, ref EntityEffectEvent<ModifyLungGas> args)
    {
        var amount = args.Scale;

        foreach (var (gas, ratio) in args.Effect.Ratios)
        {
            // DS14: resolve the gas prototype ID to its index against the live registry.
            if (!_atmos.TryGetGasId(gas, out var gasId))
                continue;

            var quantity = ratio * amount / Atmospherics.BreathMolesToReagentMultiplier;
            if (quantity < 0)
                quantity = Math.Max(quantity, -entity.Comp.Air[gasId]);
            entity.Comp.Air.AdjustMoles(gasId, quantity);
        }
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class ModifyLungGas : EntityEffectBase<ModifyLungGas>
{
    // DS14: keyed by gas prototype ID (name) so YAML-only gases work.
    [DataField(required: true)]
    public Dictionary<ProtoId<GasPrototype>, float> Ratios = default!;
}
