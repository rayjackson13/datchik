namespace SysMonitor.Core.Hardware;

/// <summary>
/// All sensor values read at one moment. Each value is null if the sensor wasn't found
/// or had no reading, so the window can show a dash instead of a misleading 0.
/// </summary>
/// <param name="CpuUtilizationPercent">Total CPU load, 0–100.</param>
/// <param name="CpuTemperatureCelsius">CPU temperature in °C.</param>
/// <param name="CpuClockMhz">Average CPU core clock in MHz.</param>
/// <param name="CpuPowerWatts">CPU package power in watts.</param>
/// <param name="RamUsedGigabytes">RAM in use, in GB.</param>
/// <param name="RamTotalGigabytes">Installed RAM, in GB.</param>
/// <param name="GpuUtilizationPercent">GPU core load, 0–100.</param>
/// <param name="GpuTemperatureCelsius">GPU core temperature in °C.</param>
/// <param name="GpuClockMhz">GPU core clock in MHz.</param>
/// <param name="GpuVoltageVolts">GPU core voltage in volts.</param>
/// <param name="GpuPowerWatts">GPU power in watts.</param>
/// <param name="GpuFanRpm">GPU fan speed in RPM.</param>
/// <param name="GpuMemoryUsedMegabytes">Video memory in use, in MB.</param>
/// <param name="GpuMemoryTotalMegabytes">Total video memory, in MB.</param>
/// <param name="MotherboardFans">
/// Every fan header on the motherboard with its speed. The App decides which one is the CPU fan.
/// </param>
// IReadOnlyList<T> = a list that can be read but not changed. Whoever receives the snapshot
// can't accidentally add or remove fans from it.
public record MetricsSnapshot(
    float? CpuUtilizationPercent,
    float? CpuTemperatureCelsius,
    float? CpuClockMhz,
    float? CpuPowerWatts,
    double? RamUsedGigabytes,
    double? RamTotalGigabytes,
    float? GpuUtilizationPercent,
    float? GpuTemperatureCelsius,
    float? GpuClockMhz,
    float? GpuVoltageVolts,
    float? GpuPowerWatts,
    float? GpuFanRpm,
    float? GpuMemoryUsedMegabytes,
    float? GpuMemoryTotalMegabytes,
    IReadOnlyList<FanSpeed> MotherboardFans);
