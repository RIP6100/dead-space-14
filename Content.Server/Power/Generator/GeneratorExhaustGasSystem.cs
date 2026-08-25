using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Power.Generator;

namespace Content.Server.Power.Generator;

/// <seealso cref="GeneratorSystem"/>
/// <seealso cref="GeneratorExhaustGasComponent"/>
public sealed class GeneratorExhaustGasSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GeneratorExhaustGasComponent, GeneratorUseFuel>(FuelUsed);
    }

    private void FuelUsed(EntityUid uid, GeneratorExhaustGasComponent component, GeneratorUseFuel args)
    {
        // DS14: resolve the exhaust gas prototype ID to its index against the live registry.
        if (!_atmosphere.TryGetGasId(component.GasType, out var gasId))
            return;

        var exhaustMixture = new GasMixture();
        exhaustMixture.SetMoles(gasId, args.FuelUsed * component.MoleRatio);
        exhaustMixture.Temperature = component.Temperature;

        var environment = _atmosphere.GetContainingMixture(uid, false, true);
        if (environment != null)
            _atmosphere.Merge(environment, exhaustMixture);
    }
}
