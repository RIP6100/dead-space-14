using Content.Server.Atmos.EntitySystems;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Atmos;

namespace Content.Server.EntityEffects.Effects.Atmos;

/// <summary>
/// This effect adjusts a gas at the tile this entity is currently on.
/// The amount changed is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class CreateGasEntityEffectSystem : EntityEffectSystem<TransformComponent, CreateGas>
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<CreateGas> args)
    {
        var tileMix = _atmosphere.GetContainingMixture(entity.AsNullable(), false, true);

        // DS14: resolve the gas prototype ID to its index against the live registry.
        if (tileMix != null && _atmosphere.TryGetGasId(args.Effect.Gas, out var gasId))
            tileMix.AdjustMoles(gasId, args.Scale * args.Effect.Moles);
    }
}
