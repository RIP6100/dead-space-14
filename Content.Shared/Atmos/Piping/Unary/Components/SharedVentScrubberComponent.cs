using Content.Shared.Atmos.Monitor.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Piping.Unary.Components
{
    [Serializable, NetSerializable]
    public sealed class GasVentScrubberData : IAtmosDeviceData
    {
        public bool Enabled { get; set; }
        public bool Dirty { get; set; }
        public bool IgnoreAlarms { get; set; } = false;
        // DS14: gas indices (see GasSetSerializer) instead of the Gas enum, so gases added purely in YAML can be scrubbed.
        public HashSet<int> FilterGases { get; set; } = new(DefaultFilterGases);
        public ScrubberPumpDirection PumpDirection { get; set; } = ScrubberPumpDirection.Scrubbing;
        public float VolumeRate { get; set; } = 200f;
        public bool WideNet { get; set; } = false;
        public bool AirAlarmPanicWireCut { get; set; }

        /// <summary>
        ///     The gases a scrubber filters by default: every gas that is not flagged
        ///     <see cref="Prototypes.GasPrototype.Common"/> (i.e. everything except roundstart air like O2/N2).
        /// </summary>
        /// <remarks>
        ///     DS14: populated from the gas prototypes in
        ///     <see cref="EntitySystems.SharedAtmosphereSystem.InitializeGases"/> so new gases are scrubbed by default
        ///     automatically. It is a plain static (no IoC access) because scrubber components are constructed on
        ///     worker threads during parallel prototype loading, where IoC has no context.
        /// </remarks>
        public static HashSet<int> DefaultFilterGases = new();

        // Presets for 'dumb' air alarm modes. Lazy so DefaultFilterGases is only evaluated once gases are loaded.

        public static GasVentScrubberData FilterModePreset => new()
        {
            Enabled = true,
            FilterGases = new(DefaultFilterGases),
            PumpDirection = ScrubberPumpDirection.Scrubbing,
            VolumeRate = 200f,
            WideNet = false
        };

        public static GasVentScrubberData WideFilterModePreset => new()
        {
            Enabled = true,
            FilterGases = new(DefaultFilterGases),
            PumpDirection = ScrubberPumpDirection.Scrubbing,
            VolumeRate = 200f,
            WideNet = true
        };

        public static GasVentScrubberData FillModePreset => new()
        {
            Enabled = false,
            Dirty = true,
            FilterGases = new(DefaultFilterGases),
            PumpDirection = ScrubberPumpDirection.Scrubbing,
            VolumeRate = 200f,
            WideNet = false
        };

        public static GasVentScrubberData PanicModePreset => new()
        {
            Enabled = true,
            Dirty = true,
            FilterGases = new(DefaultFilterGases),
            PumpDirection = ScrubberPumpDirection.Siphoning,
            VolumeRate = 200f,
            WideNet = true
        };

        public static GasVentScrubberData ReplaceModePreset => new()
        {
            Enabled = true,
            IgnoreAlarms = true,
            Dirty = true,
            FilterGases = new(DefaultFilterGases),
            PumpDirection = ScrubberPumpDirection.Siphoning,
            VolumeRate = 200f,
            WideNet = false
        };
    }

    [Serializable, NetSerializable]
    public enum ScrubberPumpDirection : sbyte
    {
        Siphoning = 0,
        Scrubbing = 1,
    }
}
