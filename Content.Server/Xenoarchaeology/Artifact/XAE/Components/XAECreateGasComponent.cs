using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.Artifact.XAE.Components;

/// <summary>
/// XenoArtifact effect that creates gas in atmosphere.
/// </summary>
[RegisterComponent, Access(typeof(XAECreateGasSystem))]
public sealed partial class XAECreateGasComponent : Component
{
    /// <summary>
    /// The gases and how many moles will be created of each.
    /// </summary>
    // DS14: keyed by gas prototype ID (name) so YAML-only gases work.
    [DataField]
    public Dictionary<ProtoId<GasPrototype>, float> Gases = new();
}
