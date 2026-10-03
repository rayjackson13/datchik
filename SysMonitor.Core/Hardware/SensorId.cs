using LibreHardwareMonitor.Hardware;

// File-scoped namespace: everything in this file belongs to "SysMonitor.Core".
// Namespaces group related types, like folders. The App project imports this one with
// "using SysMonitor.Core;".
namespace SysMonitor.Core.Hardware;

/// <summary>
/// Identifies one sensor in LibreHardwareMonitor's hardware tree: which kind of hardware
/// it's on, what it measures, and its exact name as LHM reports it.
/// </summary>
/// <param name="HardwareType">The kind of hardware the sensor belongs to, e.g. Cpu, GpuNvidia, SuperIO.</param>
/// <param name="SensorType">What the sensor measures, e.g. Load, Temperature, Fan.</param>
/// <param name="Name">The sensor's name exactly as LHM reports it, e.g. "CPU Total".</param>
// "public" = usable from other projects (the App). Without it, only Core could see this type.
public record SensorId(HardwareType HardwareType, SensorType SensorType, string Name);
