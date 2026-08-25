using System.Runtime.CompilerServices;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.EntitySystems;

public abstract partial class SharedAtmosphereSystem
{
    /*
     Partial class for operations involving GasMixtures.

     Sometimes methods here are abstract because they need different client/server implementations
     due to sandboxing.
     */

    /// <summary>
    /// Cached array of gas specific heats.
    /// </summary>
    public float[] GasSpecificHeats => _gasSpecificHeats;
    private float[] _gasSpecificHeats = new float[Atmospherics.AdjustedNumberOfGases]; // DS14: fixed capacity length

    // DS14: sized to the runtime gas count in InitializeGases (no longer a fixed compile-time length).
    public string?[] GasReagents = new string[Atmospherics.TotalNumberOfGases];
    protected GasPrototype[] GasPrototypes = new GasPrototype[Atmospherics.TotalNumberOfGases];

    // DS14-start: runtime gas registry (data-driven gas rework, phase 0).
    // Maps a gas prototype ID (e.g. "Oxygen") to its stable integer index in mole arrays.
    // Built once at init so the rest of the code can resolve gases by prototype ID without touching the Gas enum.
    private readonly Dictionary<string, int> _gasIndices = new();

    /// <summary>
    /// Number of registered gases, computed from the loaded gas prototypes. This is the authoritative runtime
    /// count and drives <see cref="Atmospherics.TotalNumberOfGases"/> / <see cref="Atmospherics.AdjustedNumberOfGases"/>.
    /// </summary>
    public int GasCount { get; private set; }

    /// <summary>
    /// Resolves a gas prototype ID to its stable integer index in <see cref="GasMixture.Moles"/>.
    /// </summary>
    public int GetGasId(string protoId) => _gasIndices[protoId];

    /// <summary>
    /// Tries to resolve a gas prototype ID to its stable integer index. Returns false for unknown gases.
    /// </summary>
    public bool TryGetGasId(string protoId, out int id) => _gasIndices.TryGetValue(protoId, out id);
    // DS14-end

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        // Rebuild the gas registry (and, on the server, the reaction cache) when gas or reaction prototypes change.
        if (args.WasModified<GasPrototype>() || WereReactionsModified(args))
            InitializeGases();
    }

    /// <summary>
    /// Whether gas reaction prototypes were part of a prototype reload. Overridden on the server, where the
    /// reaction prototype type lives.
    /// </summary>
    protected virtual bool WereReactionsModified(PrototypesReloadedEventArgs args) => false;

    public virtual void InitializeGases()
    {
        // DS14: the gas prototypes are now the source of truth for the gas count and indices, not the Gas enum.
        // Reset the registry so re-init (e.g. integration test instances) is idempotent.
        _gasIndices.Clear();

        // Pass 1: resolve every gas prototype's array index and find the highest one, so we know how big the
        // runtime gas arrays need to be.
        var resolved = new List<(GasPrototype Proto, int Index)>();
        var maxIndex = -1;
        foreach (var gasPrototype in _prototypeManager.EnumeratePrototypes<GasPrototype>())
        {
            // The array index comes from the prototype. Prototypes without an explicit index fall back to their
            // matching Gas enum value, so nothing breaks while gases are being migrated off the enum.
            var idx = gasPrototype.Index;
            if (idx < 0)
            {
                if (!Enum.TryParse<Gas>(gasPrototype.ID, out var gasEnum))
                {
                    Log.Error($"GasPrototype \"{gasPrototype.ID}\" has no explicit index and does not match any {nameof(Gas)} enum value. Skipping.");
                    continue;
                }
                idx = (int)gasEnum;
            }

            resolved.Add((gasPrototype, idx));
            if (idx > maxIndex)
                maxIndex = idx;
        }

        GasCount = maxIndex + 1;

        // The mole arrays are a fixed length (allocated during prototype load, before this runs), so the gas count
        // must fit inside that capacity. Fail loudly rather than corrupt mixtures of mismatched lengths later.
        if (GasCount > Atmospherics.AdjustedNumberOfGases)
        {
            throw new InvalidOperationException(
                $"There are {GasCount} gases (highest index {maxIndex}) but the gas array capacity is only " +
                $"{Atmospherics.AdjustedNumberOfGases}. Increase {nameof(Atmospherics)}.{nameof(Atmospherics.MaxNumberOfGases)}.");
        }

        // Publish the runtime gas count so gas mixtures and loops use the right number of gases.
        // AdjustedNumberOfGases (the array length) is intentionally NOT changed here.
        Atmospherics.TotalNumberOfGases = GasCount;

        // Registry arrays are indexed directly by gas id, so they only need GasCount entries.
        // The specific-heat array is used in SIMD math against mole arrays, so it must match AdjustedNumberOfGases.
        GasPrototypes = new GasPrototype[GasCount];
        GasReagents = new string?[GasCount];
        _gasSpecificHeats = new float[Atmospherics.AdjustedNumberOfGases];

        // Pass 2: place each gas at its resolved index.
        foreach (var (proto, idx) in resolved)
        {
            if (GasPrototypes[idx] != null)
            {
                Log.Error($"GasPrototype \"{proto.ID}\" reuses index {idx} already taken by \"{GasPrototypes[idx].ID}\". Skipping.");
                continue;
            }

            GasPrototypes[idx] = proto;
            GasReagents[idx] = proto.Reagent;
            _gasIndices[proto.ID] = idx;
        }

        // Gas indices must be contiguous (0..GasCount-1). A gap would leave a null slot that lots of gas-iterating
        // code dereferences, so reject it up front with a clear message instead of a mystery NullReferenceException.
        for (var i = 0; i < GasCount; i++)
        {
            if (GasPrototypes[i] == null)
            {
                throw new InvalidOperationException(
                    $"Gas index {i} is missing. Gas prototype indices must be contiguous starting at 0 " +
                    $"(highest declared index is {maxIndex}).");
            }
        }

        for (var i = 0; i < GasPrototypes.Length; i++)
        {
            /*
             As an optimization routine we pre-divide the specific heat by the heat scale here,
             so we don't have to do it every time we calculate heat capacity.
             Most usages are going to want the scaled value anyway.

             If you would like the unscaled specific heat, you'd need to multiply by HeatScale again.
             TODO ATMOS: please just make this 2 separate arrays instead of invoking multiplication every time.
             */
            _gasSpecificHeats[i] = GasPrototypes[i].SpecificHeat / HeatScale;
        }

        // DS14: default scrubber/filter set = every non-common gas (i.e. everything except roundstart air).
        // Populated here (main thread, prototypes loaded) so components don't need IoC access at construction.
        var defaultFilterGases = new HashSet<int>();
        for (var i = 0; i < GasPrototypes.Length; i++)
        {
            if (GasPrototypes[i] is { Common: false })
                defaultFilterGases.Add(i);
        }
        GasVentScrubberData.DefaultFilterGases = defaultFilterGases;
    }

    /// <summary>
    /// Calculates the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the heat capacity for.</param>
    /// <param name="applyScaling">Whether to apply the heat capacity scaling factor.
    /// This is an extremely important boolean to consider or else you will get heat transfer wrong.
    /// See <see cref="CCVars.AtmosHeatScale"/> for more info.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    [PublicAPI]
    public float GetHeatCapacity(GasMixture mixture, bool applyScaling)
    {
        var scale = GetHeatCapacityCalculation(mixture.Moles, mixture.Immutable);

        // By default GetHeatCapacityCalculation() has the heat-scale divisor pre-applied.
        // So if we want the un-scaled heat capacity, we have to multiply by the scale.
        return applyScaling ? scale : scale * HeatScale;
    }

    /// <summary>
    /// Gets the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="mixture">The <see cref="GasMixture"/> to calculate the heat capacity for.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    /// <remarks>Note that the heat capacity of the mixture may be slightly different from
    /// "real life" as we intentionally fake a heat capacity for space in <see cref="Atmospherics.SpaceHeatCapacity"/>
    /// in order to allow Atmospherics to cool down space.</remarks>
    protected float GetHeatCapacity(GasMixture mixture)
    {
        return GetHeatCapacityCalculation(mixture.Moles, mixture.Immutable);
    }

    /// <summary>
    /// Gets the heat capacity for a <see cref="GasMixture"/>.
    /// </summary>
    /// <param name="moles">The moles array of the <see cref="GasMixture"/></param>
    /// <param name="space">Whether this <see cref="GasMixture"/> represents space,
    /// and thus experiences space-specific mechanics (we cheat and make it a bit cooler).
    /// See <see cref="Atmospherics.SpaceHeatCapacity"/>.</param>
    /// <returns>The heat capacity of the <see cref="GasMixture"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract float GetHeatCapacityCalculation(float[] moles, bool space);
}
