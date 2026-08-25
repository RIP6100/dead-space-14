using Content.Shared.Atmos.Prototypes;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.EntitySystems
{
    public abstract partial class SharedAtmosphereSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedInternalsSystem _internals = default!;
        [Dependency] private readonly IConfigurationManager _cfg = default!;

        private EntityQuery<InternalsComponent> _internalsQuery;

        public override void Initialize()
        {
            base.Initialize();

            _internalsQuery = GetEntityQuery<InternalsComponent>();

            InitializeBreathTool();
            InitializeGases();
            InitializeCVars();

            // DS14: gases are data-driven, so rebuild the gas registry if gas prototypes are (re)loaded
            // (e.g. admin hot-reload, or integration tests that inject extra gas prototypes).
            _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;
        }

        public GasPrototype GetGas(int gasId) => GasPrototypes[gasId];

        public GasPrototype GetGas(Gas gasId) => GasPrototypes[(int) gasId];

        // DS14: resolve a gas by prototype ID (for gases referenced by name, including YAML-only gases).
        public GasPrototype GetGas(ProtoId<GasPrototype> gasId) => GasPrototypes[GetGasId(gasId)];

        public IEnumerable<GasPrototype> Gases => GasPrototypes;
    }
}
