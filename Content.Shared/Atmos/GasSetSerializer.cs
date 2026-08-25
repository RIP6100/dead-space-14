using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Content.Shared.Atmos;

/// <summary>
/// DS14: serializes a set of gases (e.g. a scrubber filter) written in YAML as a sequence of gas names into a
/// <see cref="HashSet{T}"/> of stable gas indices. Keeps YAML readable (gas names) while the runtime works with
/// indices, and supports gases added purely through YAML with no <see cref="Gas"/> enum entry.
/// </summary>
public sealed class GasSetSerializer : ITypeSerializer<HashSet<int>, SequenceDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        var list = new List<ValidationNode>();

        foreach (var elem in node.Sequence)
        {
            var key = elem.ToString() ?? string.Empty;
            list.Add(GasSerializationHelper.TryResolveGasIndex(dependencies, key, out _)
                ? new ValidatedValueNode(elem)
                : new ErrorNode(elem, $"Failed to resolve Gas: {key}"));
        }

        return new ValidatedSequenceNode(list);
    }

    public HashSet<int> Read(ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<HashSet<int>>? instanceProvider = null)
    {
        var set = instanceProvider != null ? instanceProvider() : new HashSet<int>();

        foreach (var elem in node.Sequence)
        {
            var key = elem.ToString() ?? string.Empty;
            // Errors are already reported by Validate(); silently skip unknown gases here.
            if (GasSerializationHelper.TryResolveGasIndex(dependencies, key, out var index))
                set.Add(index);
        }

        return set;
    }

    public DataNode Write(ISerializationManager serializationManager,
        HashSet<int> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var idByIndex = GasSerializationHelper.BuildIndexToIdMap(dependencies.Resolve<IPrototypeManager>());
        var sequence = new SequenceDataNode();

        foreach (var index in value)
        {
            var key = idByIndex.TryGetValue(index, out var id)
                ? id
                : Enum.IsDefined((Gas)index) ? ((Gas)index).ToString() : index.ToString();

            sequence.Add(new ValueDataNode(key));
        }

        return sequence;
    }
}
