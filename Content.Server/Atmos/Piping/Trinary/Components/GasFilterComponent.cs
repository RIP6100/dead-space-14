using Content.Shared.Atmos;
using Content.Shared.Atmos.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Atmos.Piping.Trinary.Components
{
    [RegisterComponent]
    public sealed partial class GasFilterComponent : Component
    {
        [DataField]
        public bool Enabled = true;

        [DataField("inlet")]
        public string InletName = "inlet";

        [DataField("filter")]
        public string FilterName = "filter";

        [DataField("outlet")]
        public string OutletName = "outlet";

        [DataField]
        public float TransferRate = Atmospherics.MaxTransferRate;

        [DataField]
        public float MaxTransferRate = Atmospherics.MaxTransferRate;

        // DS14: gas prototype ID (not the Gas enum) so gases added purely in YAML can be filtered.
        // Authored/persisted by gas name (e.g. "CarbonDioxide"); resolved to an index at runtime.
        [DataField]
        public ProtoId<GasPrototype>? FilteredGas;
    }
}
