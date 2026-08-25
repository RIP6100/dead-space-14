using System.Linq;
using Content.Server.Botany.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Botany;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.EntityEffects.Effects.Botany;

public sealed partial class PlantMutateExudeGasesEntityEffectSystem : EntityEffectSystem<PlantHolderComponent, PlantMutateExudeGases>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!; // DS14: pick random gas from the registry

    protected override void Effect(Entity<PlantHolderComponent> entity, ref EntityEffectEvent<PlantMutateExudeGases> args)
    {
        if (entity.Comp.Seed == null)
            return;

        var gasses = entity.Comp.Seed.ExudeGasses;

        // Add a random amount of a random gas to this gas dictionary
        float amount = _random.NextFloat(args.Effect.MinValue, args.Effect.MaxValue);
        // DS14: pick a random gas prototype (works for gases added purely in YAML) instead of the Gas enum.
        var gas = _random.Pick(_proto.EnumeratePrototypes<GasPrototype>().ToList()).ID;

        if (!gasses.TryAdd(gas, amount))
        {
            gasses[gas] += amount;
        }
    }
}

public sealed partial class PlantMutateConsumeGasesEntityEffectSystem : EntityEffectSystem<PlantHolderComponent, PlantMutateConsumeGases>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!; // DS14: pick random gas from the registry

    protected override void Effect(Entity<PlantHolderComponent> entity, ref EntityEffectEvent<PlantMutateConsumeGases> args)
    {
        if (entity.Comp.Seed == null)
            return;

        var gasses = entity.Comp.Seed.ConsumeGasses;

        // Add a random amount of a random gas to this gas dictionary
        var amount = _random.NextFloat(args.Effect.MinValue, args.Effect.MaxValue);
        // DS14: pick a random gas prototype (works for gases added purely in YAML) instead of the Gas enum.
        var gas = _random.Pick(_proto.EnumeratePrototypes<GasPrototype>().ToList()).ID;

        if (!gasses.TryAdd(gas, amount))
        {
            gasses[gas] += amount;
        }
    }
}

