using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server.Atmos.Reactions;

/// <summary>
///     Generic, fully data-driven stoichiometric gas reaction. Lets new reactions be authored entirely in YAML
///     without writing a bespoke C# effect class.
/// </summary>
/// <remarks>
///     <para>The reaction extent each tick (ξ, "moles of reaction") is:</para>
///     <code>ξ = rate · concentrationFactor · limit</code>
///     <para>where:</para>
///     <list type="bullet">
///         <item><c>limit</c> is <c>min(moles[g] / reactants[g])</c> over every reactant (the true limiting
///         reactant), OR <c>moles[limitingReactant] / reactants[limitingReactant]</c> if an explicit limiting
///         reactant is given. The explicit form reproduces legacy reactions that intentionally base their rate on
///         a single reactant and let the others clamp to zero.</item>
///         <item><c>concentrationFactor</c> is <c>Π (moles[g] / totalMoles)^exp</c> over gases with a non-zero
///         concentration exponent. Gases with no exponent contribute a factor of 1.</item>
///     </list>
///     <para>Each reactant then loses <c>ξ · coeff</c> moles and each product gains <c>ξ · coeff</c> moles.
///     Optionally releases <c>energyPerReaction · ξ</c> joules (negative cools the mixture) and/or exposes a
///     hotspot when hot enough.</para>
///     <para>DS14: core of the data-driven gas reaction rework. Gases are referenced by prototype ID and resolved
///     to indices lazily on first reaction (against the live gas registry), so gases added purely in YAML work and
///     there is no prototype load-order fragility. Optional blocks (<see cref="TemperatureScale"/>,
///     <see cref="ProductSplit"/>) cover temperature ramps and product splitting. Nonlinear reactions that can't be
///     composed from blocks (plasma fire, tritium fire, frezon production) are selected via <see cref="SpecialEffect"/>,
///     which runs a built-in routine parameterised entirely from YAML.</para>
/// </remarks>
[UsedImplicitly]
[DataDefinition]
public sealed partial class GasRecipeReaction : IGasReactionEffect
{
    /// <summary>Moles consumed per mole of reaction, keyed by gas prototype ID.</summary>
    [DataField]
    public Dictionary<ProtoId<GasPrototype>, float> Reactants = new();

    /// <summary>Moles produced per mole of reaction, keyed by gas prototype ID.</summary>
    [DataField]
    public Dictionary<ProtoId<GasPrototype>, float> Products = new();

    /// <summary>
    ///     Per-gas exponents for the concentration-dependent rate factor, keyed by gas prototype ID. Leave empty
    ///     for a rate that does not depend on how concentrated the gases are.
    /// </summary>
    [DataField]
    public Dictionary<ProtoId<GasPrototype>, float> Concentration = new();

    /// <summary>Base fraction of the limiting reactant that reacts each tick.</summary>
    [DataField]
    public float Rate = 1f;

    /// <summary>
    ///     If set, the reaction extent is limited by this single reactant only (legacy behaviour for reactions
    ///     that intentionally over-consume the other reactants). If null, the true limiting reactant is used.
    /// </summary>
    [DataField]
    public ProtoId<GasPrototype>? LimitingReactant;

    /// <summary>Joules released per mole of reaction. Negative values cool the mixture (endothermic).</summary>
    [DataField]
    public float EnergyPerReaction;

    /// <summary>Whether to expose the tile to a hotspot (fire) after reacting, if hot enough.</summary>
    [DataField]
    public bool Hotspot;

    /// <summary>
    ///     Optional block: scales the reaction rate with temperature between two points
    ///     (below <see cref="GasTemperatureScaleBlock.Min"/> the reaction stops).
    /// </summary>
    [DataField]
    public GasTemperatureScaleBlock? TemperatureScale;

    /// <summary>
    ///     Optional block: splits one unit of product between two gases based on the ratio of two reactants
    ///     (e.g. plasma fire's supersaturation into tritium vs CO2).
    /// </summary>
    [DataField]
    public GasProductSplitBlock? ProductSplit;

    /// <summary>
    ///     Selects a built-in nonlinear reaction routine whose math can't be expressed by the generic blocks
    ///     (plasma fire, tritium fire, frezon production). When set, the generic block fields are ignored and the
    ///     matching <c>Special*</c> parameters below are used instead. Everything is still authored in YAML.
    /// </summary>
    [DataField]
    public GasSpecialEffect SpecialEffect = GasSpecialEffect.None;

    #region specialEffect parameters (defaults preserve vanilla behaviour)

    // Plasma fire
    [DataField] public float UpperTemperature = Atmospherics.PlasmaUpperTemperature;
    [DataField] public float MinimumBurnTemperature = Atmospherics.PlasmaMinimumBurnTemperature;
    [DataField] public float OxygenBurnRateBase = Atmospherics.OxygenBurnRateBase;
    [DataField] public float SuperSaturationEnds = Atmospherics.SuperSaturationEnds;
    [DataField] public float SuperSaturationThreshold = Atmospherics.SuperSaturationThreshold;
    [DataField] public float OxygenFullburn = Atmospherics.PlasmaOxygenFullburn;
    [DataField] public float BurnRateDelta = Atmospherics.PlasmaBurnRateDelta;

    // Tritium fire
    [DataField] public float MinimumOxyburnEnergy = Atmospherics.MinimumTritiumOxyburnEnergy;
    [DataField] public float BurnOxyFactor = Atmospherics.TritiumBurnOxyFactor;
    [DataField] public float BurnFuelRatio = Atmospherics.TritiumBurnFuelRatio;
    [DataField] public float BurnTritFactor = Atmospherics.TritiumBurnTritFactor;

    /// <summary>Joules released per mole of fuel burnt (plasma fire and tritium fire).</summary>
    [DataField] public float EnergyReleased;

    // Frezon production
    [DataField] public float MaxEfficiencyTemperature = Atmospherics.FrezonProductionMaxEfficiencyTemperature;
    [DataField] public float NitrogenRatio = Atmospherics.FrezonProductionNitrogenRatio;
    [DataField] public float TritRatio = Atmospherics.FrezonProductionTritRatio;
    [DataField] public float ConversionRate = Atmospherics.FrezonProductionConversionRate;

    #endregion

    // Compiled, index-based form built once on first reaction (the gas registry is fully populated by then).
    private bool _compiled;
    private (int Gas, float Coeff)[] _reactants = [];
    private (int Gas, float Coeff)[] _products = [];
    private (int Gas, float Exp)[] _concentration = [];
    private int _limitingId = -1;
    private int _splitNum = -1, _splitDen = -1, _splitLow = -1, _splitHigh = -1;

    private void Compile(AtmosphereSystem atmosphereSystem)
    {
        _reactants = Resolve(Reactants, atmosphereSystem);
        _products = Resolve(Products, atmosphereSystem);
        _concentration = Resolve(Concentration, atmosphereSystem);
        _limitingId = LimitingReactant != null && atmosphereSystem.TryGetGasId(LimitingReactant.Value, out var id)
            ? id
            : -1;

        if (ProductSplit != null
            && ProductSplit.RatioOf.Count >= 2
            && atmosphereSystem.TryGetGasId(ProductSplit.RatioOf[0], out _splitNum)
            && atmosphereSystem.TryGetGasId(ProductSplit.RatioOf[1], out _splitDen)
            && atmosphereSystem.TryGetGasId(ProductSplit.LowProduct, out _splitLow)
            && atmosphereSystem.TryGetGasId(ProductSplit.HighProduct, out _splitHigh))
        {
            // all resolved
        }
        else
        {
            _splitLow = -1; // disable if misconfigured (needs exactly two ratio gases) or any gas failed to resolve
        }

        _compiled = true;
    }

    private static (int Gas, float Coeff)[] Resolve(Dictionary<ProtoId<GasPrototype>, float> source, AtmosphereSystem atmosphereSystem)
    {
        var list = new List<(int, float)>(source.Count);
        foreach (var (gas, coeff) in source)
        {
            if (atmosphereSystem.TryGetGasId(gas, out var id))
                list.Add((id, coeff));
        }

        return list.ToArray();
    }

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        if (!_compiled)
            Compile(atmosphereSystem);

        // Built-in nonlinear routines that the generic blocks can't express.
        switch (SpecialEffect)
        {
            case GasSpecialEffect.PlasmaFire:
                return ReactPlasmaFire(mixture, holder, atmosphereSystem, heatScale);
            case GasSpecialEffect.TritiumFire:
                return ReactTritiumFire(mixture, holder, atmosphereSystem, heatScale);
            case GasSpecialEffect.FrezonProduction:
                return ReactFrezonProduction(mixture);
        }

        // Determine reaction extent, limited by available reactants.
        float limit;
        if (_limitingId >= 0)
        {
            var coeff = 0f;
            foreach (var (gas, c) in _reactants)
            {
                if (gas == _limitingId)
                {
                    coeff = c;
                    break;
                }
            }

            if (coeff <= 0)
                return ReactionResult.NoReaction;
            limit = mixture.GetMoles(_limitingId) / coeff;
        }
        else
        {
            if (_reactants.Length == 0)
                return ReactionResult.NoReaction;

            limit = float.MaxValue;
            foreach (var (gas, coeff) in _reactants)
            {
                if (coeff <= 0)
                    continue;
                var ratio = mixture.GetMoles(gas) / coeff;
                if (ratio < limit)
                    limit = ratio;
            }
        }

        var temperature = mixture.Temperature;
        var extent = Rate * limit;
        var energyMultiplier = 1f;

        // temperatureScale block: rate ramps from 0 (at/below Min) to 1 (at/above Max).
        if (TemperatureScale != null)
        {
            var span = TemperatureScale.Max - TemperatureScale.Min;
            // Guard against a misconfigured (Max <= Min) range: fall back to a step at Max instead of dividing by zero.
            var raw = span > 0f
                ? (temperature - TemperatureScale.Min) / span
                : (temperature >= TemperatureScale.Max ? 1f : 0f);
            var factor = Math.Clamp(raw, 0f, 1f);
            if (factor <= 0f)
                return ReactionResult.NoReaction;

            extent *= factor;

            // Optional overshoot: above Max the rate stays capped at 1, but energy keeps scaling (up to the cap).
            if (TemperatureScale.EnergyOvershootCap is { } cap && raw > 1f)
                energyMultiplier = MathF.Min(raw, cap);
        }

        // Concentration-dependent rate factor (only touched if any exponent is set).
        if (_concentration.Length > 0)
        {
            var total = mixture.TotalMoles;
            if (total <= 0)
                return ReactionResult.NoReaction;

            foreach (var (gas, exp) in _concentration)
            {
                if (exp != 0)
                    extent *= MathF.Pow(mixture.GetMoles(gas) / total, exp);
            }
        }

        if (extent <= 0)
            return ReactionResult.NoReaction;

        // productSplit block: capture the split fraction from the pre-reaction reactant ratio.
        var splitFraction = 0f;
        if (_splitLow >= 0)
        {
            var denominator = mixture.GetMoles(_splitDen);
            var ratio = denominator > 0f ? mixture.GetMoles(_splitNum) / denominator : float.MaxValue;
            var span = ProductSplit!.To - ProductSplit.From;
            // Guard against a misconfigured (To == From) range: fall back to a step at To instead of dividing by zero.
            splitFraction = span != 0f
                ? Math.Clamp((ratio - ProductSplit.From) / span, 0f, 1f)
                : (ratio >= ProductSplit.To ? 1f : 0f);
        }

        // Capture the pre-reaction heat state only when we actually release/absorb energy.
        var oldHeatCapacity = EnergyPerReaction != 0f ? atmosphereSystem.GetHeatCapacity(mixture, true) : 0f;

        foreach (var (gas, coeff) in _reactants)
        {
            if (coeff > 0)
                mixture.AdjustMoles(gas, -extent * coeff);
        }

        foreach (var (gas, coeff) in _products)
        {
            if (coeff > 0)
                mixture.AdjustMoles(gas, extent * coeff);
        }

        // productSplit block: divide one unit of product between the low/high gases by the captured fraction.
        if (_splitLow >= 0)
        {
            mixture.AdjustMoles(_splitLow, extent * (1f - splitFraction));
            mixture.AdjustMoles(_splitHigh, extent * splitFraction);
        }

        if (EnergyPerReaction != 0f)
        {
            // Adjust energy by heat scale so atmos speedup doesn't cause a runaway temperature swing.
            var energyReleased = EnergyPerReaction * extent * energyMultiplier / heatScale;
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
        }

        if (Hotspot && holder is TileAtmosphere location)
        {
            var mixTemperature = mixture.Temperature;
            if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                atmosphereSystem.HotspotExpose(location, mixTemperature, mixture.Volume);
        }

        return ReactionResult.Reacting;
    }

    // ---- Built-in special routines (ported from the former dedicated effect classes) ----

    private ReactionResult ReactPlasmaFire(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0;

        var temperatureScale = temperature > UpperTemperature
            ? 1f
            : (temperature - MinimumBurnTemperature) / (UpperTemperature - MinimumBurnTemperature);

        if (temperatureScale > 0)
        {
            var oxygenBurnRate = OxygenBurnRateBase - temperatureScale;
            var plasmaBurnRate = 0f;

            var initialOxygenMoles = mixture.GetMoles(Gas.Oxygen);
            var initialPlasmaMoles = mixture.GetMoles(Gas.Plasma);

            var oxyRatio = initialOxygenMoles / initialPlasmaMoles;
            var supersaturation = Math.Clamp((oxyRatio - SuperSaturationEnds) /
                                             (SuperSaturationThreshold - SuperSaturationEnds), 0.0f, 1.0f);

            if (initialOxygenMoles > initialPlasmaMoles * OxygenFullburn)
                plasmaBurnRate = initialPlasmaMoles * temperatureScale / BurnRateDelta;
            else
                plasmaBurnRate = temperatureScale * (initialOxygenMoles / OxygenFullburn) / BurnRateDelta;

            if (plasmaBurnRate > Atmospherics.MinimumHeatCapacity)
            {
                plasmaBurnRate = MathF.Min(plasmaBurnRate, MathF.Min(initialPlasmaMoles, initialOxygenMoles / oxygenBurnRate));
                mixture.SetMoles(Gas.Plasma, initialPlasmaMoles - plasmaBurnRate);
                mixture.SetMoles(Gas.Oxygen, initialOxygenMoles - plasmaBurnRate * oxygenBurnRate);

                mixture.AdjustMoles(Gas.Tritium, plasmaBurnRate * supersaturation);
                mixture.AdjustMoles(Gas.CarbonDioxide, plasmaBurnRate * (1.0f - supersaturation));

                energyReleased += EnergyReleased * plasmaBurnRate;
                energyReleased /= heatScale;
                mixture.ReactionResults[(byte)GasReaction.Fire] += plasmaBurnRate * (1 + oxygenBurnRate);
            }
        }

        if (energyReleased > 0)
        {
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;
        }

        if (location != null)
        {
            var mixTemperature = mixture.Temperature;
            if (mixTemperature > Atmospherics.FireMinimumTemperatureToExist)
                atmosphereSystem.HotspotExpose(location, mixTemperature, mixture.Volume);
        }

        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }

    private ReactionResult ReactTritiumFire(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var energyReleased = 0f;
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
        var burnedFuel = 0f;
        var initialTrit = mixture.GetMoles(Gas.Tritium);

        if (mixture.GetMoles(Gas.Oxygen) < initialTrit ||
            MinimumOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
        {
            burnedFuel = mixture.GetMoles(Gas.Oxygen) / BurnOxyFactor;
            if (burnedFuel > initialTrit)
                burnedFuel = initialTrit;

            mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
            mixture.AdjustMoles(Gas.Oxygen, -burnedFuel / BurnFuelRatio);
        }
        else
        {
            burnedFuel = Math.Min(initialTrit, mixture.GetMoles(Gas.Oxygen) / BurnFuelRatio) / BurnTritFactor;
            mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
            mixture.AdjustMoles(Gas.Oxygen, -burnedFuel / BurnFuelRatio);
            energyReleased += (EnergyReleased * burnedFuel * (BurnTritFactor - 1));
        }

        if (burnedFuel > 0)
        {
            energyReleased += (EnergyReleased * burnedFuel);

            mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);
            mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel;
        }

        energyReleased /= heatScale;
        if (energyReleased > 0)
        {
            var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
        }

        if (location != null)
        {
            temperature = mixture.Temperature;
            if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
        }

        return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
    }

    private ReactionResult ReactFrezonProduction(GasMixture mixture)
    {
        var initialN2 = mixture.GetMoles(Gas.Nitrogen);
        var initialOxy = mixture.GetMoles(Gas.Oxygen);
        var initialTrit = mixture.GetMoles(Gas.Tritium);

        var efficiency = mixture.Temperature / MaxEfficiencyTemperature;
        var loss = 1 - efficiency;

        var catalystLimit = initialN2 * (NitrogenRatio / efficiency);
        var oxyLimit = Math.Min(initialOxy, catalystLimit) / TritRatio;

        var tritBurned = Math.Min(oxyLimit, initialTrit);
        var oxyBurned = tritBurned * TritRatio;

        var oxyConversion = oxyBurned / ConversionRate;
        var tritConversion = tritBurned / ConversionRate;
        var total = oxyConversion + tritConversion;

        mixture.AdjustMoles(Gas.Oxygen, -oxyConversion);
        mixture.AdjustMoles(Gas.Tritium, -tritConversion);
        mixture.AdjustMoles(Gas.Frezon, total * efficiency);
        mixture.AdjustMoles(Gas.Nitrogen, total * loss);

        return ReactionResult.Reacting;
    }
}

/// <summary>Built-in nonlinear reaction routines selectable from YAML via <see cref="GasRecipeReaction.SpecialEffect"/>.</summary>
public enum GasSpecialEffect : byte
{
    None = 0,
    PlasmaFire,
    TritiumFire,
    FrezonProduction,
}

/// <summary>
///     temperatureScale block: multiplies the reaction rate by <c>clamp((T - Min) / (Max - Min), 0, 1)</c>.
/// </summary>
[DataDefinition]
public sealed partial class GasTemperatureScaleBlock
{
    [DataField(required: true)]
    public float Min;

    [DataField(required: true)]
    public float Max;

    /// <summary>
    ///     If set, above <see cref="Max"/> the rate stays capped at 1 but the released energy is multiplied by
    ///     <c>min((T - Min) / (Max - Min), EnergyOvershootCap)</c>. Reproduces frezon coolant's "colder space cools less".
    /// </summary>
    [DataField]
    public float? EnergyOvershootCap;
}

/// <summary>
///     productSplit block: splits one unit of product between two gases based on the ratio of two reactants.
/// </summary>
[DataDefinition]
public sealed partial class GasProductSplitBlock
{
    /// <summary>Two gases whose ratio (moles[0] / moles[1]) drives the split.</summary>
    [DataField(required: true)]
    public List<ProtoId<GasPrototype>> RatioOf = new();

    /// <summary>At/below this ratio, all product goes to <see cref="LowProduct"/>.</summary>
    [DataField(required: true)]
    public float From;

    /// <summary>At/above this ratio, all product goes to <see cref="HighProduct"/>.</summary>
    [DataField(required: true)]
    public float To;

    [DataField(required: true)]
    public ProtoId<GasPrototype> LowProduct;

    [DataField(required: true)]
    public ProtoId<GasPrototype> HighProduct;
}
