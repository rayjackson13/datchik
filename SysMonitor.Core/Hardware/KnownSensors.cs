using LibreHardwareMonitor.Hardware;

namespace SysMonitor.Core.Hardware;

/// <summary>
/// The exact sensors used for each metric on this PC (Ryzen 7 5700X, B450M Pro4-F, RTX 4070 SUPER).
/// Sensor names differ between hardware, so this will become configurable later.
/// The names came from the SensorSpike dump.
/// </summary>
public static class KnownSensors
{
    /// <summary>Total CPU load across all cores, in percent.</summary>
    public static readonly SensorId CpuUtilization = new(HardwareType.Cpu, SensorType.Load, "CPU Total");

    /// <summary>CPU temperature as a single value (not per core), in °C. "Tctl/Tdie" is AMD's naming.</summary>
    public static readonly SensorId CpuTemperature = new(HardwareType.Cpu, SensorType.Temperature, "Core (Tctl/Tdie)");

    /// <summary>Average clock of the CPU cores while they're awake, in MHz.</summary>
    public static readonly SensorId CpuClock = new(HardwareType.Cpu, SensorType.Clock, "Cores (Average)");

    /// <summary>Power drawn by the whole CPU package, in watts.</summary>
    public static readonly SensorId CpuPower = new(HardwareType.Cpu, SensorType.Power, "Package");

    /// <summary>CPU cooler fan speed, in RPM, from the motherboard chip. "Fan #1" is a guess for now.</summary>
    public static readonly SensorId CpuFan = new(HardwareType.SuperIO, SensorType.Fan, "Fan #1");

    /// <summary>GPU core load, in percent.</summary>
    public static readonly SensorId GpuUtilization = new(HardwareType.GpuNvidia, SensorType.Load, "GPU Core");

    /// <summary>GPU core temperature, in °C.</summary>
    public static readonly SensorId GpuTemperature = new(HardwareType.GpuNvidia, SensorType.Temperature, "GPU Core");

    /// <summary>GPU core clock, in MHz.</summary>
    public static readonly SensorId GpuClock = new(HardwareType.GpuNvidia, SensorType.Clock, "GPU Core");

    /// <summary>GPU core voltage, in volts.</summary>
    public static readonly SensorId GpuVoltage = new(HardwareType.GpuNvidia, SensorType.Voltage, "GPU Core Voltage");

    /// <summary>Power drawn by the GPU, in watts. ("GPU Power" is a different sensor: percent of the power limit.)</summary>
    public static readonly SensorId GpuPower = new(HardwareType.GpuNvidia, SensorType.Power, "GPU Package");

    /// <summary>GPU fan speed, in RPM. This card reports two fans; we show the first.</summary>
    public static readonly SensorId GpuFan = new(HardwareType.GpuNvidia, SensorType.Fan, "GPU Fan 1");

    /// <summary>
    /// Video memory in use, in megabytes, as reported by NVIDIA's driver (matches NVIDIA's tools).
    /// "D3D Dedicated Memory Used" is a different, slightly lower number that Task Manager shows.
    /// </summary>
    public static readonly SensorId GpuMemoryUsed = new(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Used");

    /// <summary>Total video memory on the card, in megabytes.</summary>
    public static readonly SensorId GpuMemoryTotal = new(HardwareType.GpuNvidia, SensorType.SmallData, "GPU Memory Total");
}
