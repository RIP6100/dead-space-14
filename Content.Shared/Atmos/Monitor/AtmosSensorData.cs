using Content.Shared.Atmos.Monitor.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Atmos.Monitor;

[Serializable, NetSerializable]
public sealed class AtmosSensorData : IAtmosDeviceData
{
    // DS14: Gases is keyed by gas index (not the Gas enum) so gases added purely in YAML show up in readouts.
    // GasThresholds stays keyed by the Gas enum, since alarm thresholds are authored/edited per enum gas.
    public AtmosSensorData(float pressure, float temperature, float totalMoles, AtmosAlarmType alarmState, Dictionary<int, float> gases, AtmosAlarmThreshold pressureThreshold, AtmosAlarmThreshold temperatureThreshold, Dictionary<Gas, AtmosAlarmThreshold> gasThresholds)
    {
        Pressure = pressure;
        Temperature = temperature;
        TotalMoles = totalMoles;
        AlarmState = alarmState;
        Gases = gases;
        PressureThreshold = pressureThreshold;
        TemperatureThreshold = temperatureThreshold;
        GasThresholds = gasThresholds;
    }

    public bool Enabled { get; set; }
    public bool Dirty { get; set; }
    public bool IgnoreAlarms { get; set; }

    /// Most fields are readonly, because it's data that's meant to be transmitted.

    /// <summary>
    ///     Current pressure detected by this sensor.
    /// </summary>
    public float Pressure { get; }
    /// <summary>
    ///     Current temperature detected by this sensor.
    /// </summary>
    public float Temperature { get; }
    /// <summary>
    ///     Current amount of moles detected by this sensor.
    /// </summary>
    public float TotalMoles { get; }
    /// <summary>
    ///     Current alarm state of this sensor. Does not reflect the highest alarm state on the network.
    /// </summary>
    public AtmosAlarmType AlarmState { get; }
    /// <summary>
    ///     Current amount of each gas on this sensor, keyed by gas index.
    /// </summary>
    public Dictionary<int, float> Gases { get; }

    public AtmosAlarmThreshold PressureThreshold { get; }
    public AtmosAlarmThreshold TemperatureThreshold { get; }
    public Dictionary<Gas, AtmosAlarmThreshold> GasThresholds { get; }
}
