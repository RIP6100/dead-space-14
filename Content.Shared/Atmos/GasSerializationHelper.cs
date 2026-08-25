using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos;

/// <summary>
/// DS14: shared helpers for resolving gas keys (prototype IDs) to their stable mole-array index.
/// Gas mole arrays and gas sets are keyed by <see cref="Prototypes.GasPrototype.Index"/> so gases added purely in
/// YAML (with no <see cref="Gas"/> enum entry) work everywhere. The legacy <see cref="Gas"/> enum is kept as a
/// fallback for prototypes that don't declare an explicit index yet.
/// </summary>
public static class GasSerializationHelper
{
    /// <summary>
    /// Resolves a gas key (prototype ID) to its mole-array index. Prefers the prototype's declared index,
    /// then falls back to the matching <see cref="Gas"/> enum value.
    /// </summary>
    public static bool TryResolveGasIndex(IPrototypeManager protoMan, string key, out int index)
    {
        if (protoMan.TryIndex<GasPrototype>(key, out var proto) && proto.Index >= 0)
        {
            index = proto.Index;
            return true;
        }

        if (Enum.TryParse<Gas>(key, out var gasEnum))
        {
            index = (int)gasEnum;
            return true;
        }

        index = -1;
        return false;
    }

    /// <inheritdoc cref="TryResolveGasIndex(IPrototypeManager,string,out int)"/>
    public static bool TryResolveGasIndex(IDependencyCollection dependencies, string key, out int index)
    {
        return TryResolveGasIndex(dependencies.Resolve<IPrototypeManager>(), key, out index);
    }

    /// <summary>
    /// Builds an index -> prototype ID map, so serialized gas data can be written back with the same gas keys it was
    /// read with (including gases that only exist as prototypes with no <see cref="Gas"/> enum entry).
    /// </summary>
    public static Dictionary<int, string> BuildIndexToIdMap(IPrototypeManager protoMan)
    {
        var map = new Dictionary<int, string>();

        foreach (var proto in protoMan.EnumeratePrototypes<GasPrototype>())
        {
            var idx = proto.Index;
            if (idx < 0 && Enum.TryParse<Gas>(proto.ID, out var gasEnum))
                idx = (int)gasEnum;

            if (idx >= 0)
                map[idx] = proto.ID;
        }

        return map;
    }
}
