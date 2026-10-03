using LibreHardwareMonitor.Hardware;

namespace SysMonitor.Core.Hardware;

/// <summary>
/// Reads the CPU and GPU sensors the app displays. Create one when the app starts,
/// call <see cref="ReadMetrics"/> once per refresh, and dispose it when the app closes.
/// Requires administrator rights for CPU and motherboard sensors.
/// </summary>
// "sealed"      = no other class can inherit from this one (it isn't designed for it).
// ": IDisposable" = this class promises to have a Dispose() method that releases what it holds
//                   (here: the hardware and the driver connection).
public sealed class HardwareMonitor : IDisposable
{
    // ---- Fields: data each HardwareMonitor object keeps for its whole life ----
    // "private"  = only code inside this class can use them.
    // "readonly" = set once in the constructor, never changed afterwards.
    // The "_" prefix is the usual C# naming convention for private fields, so you can tell
    // them apart from local variables at a glance.

    /// <summary>LibreHardwareMonitor's view of this PC.</summary>
    private readonly Computer _computer;

    // Each sensor we display, found once at startup. "ISensor?" = null if this PC doesn't have it.
    private readonly ISensor? _cpuUtilizationSensor;
    private readonly ISensor? _cpuTemperatureSensor;
    private readonly ISensor? _cpuClockSensor;
    private readonly ISensor? _cpuPowerSensor;
    private readonly ISensor? _cpuFanSensor;
    private readonly ISensor? _gpuUtilizationSensor;
    private readonly ISensor? _gpuTemperatureSensor;
    private readonly ISensor? _gpuClockSensor;
    private readonly ISensor? _gpuVoltageSensor;
    private readonly ISensor? _gpuPowerSensor;
    private readonly ISensor? _gpuFanSensor;
    private readonly ISensor? _gpuMemoryUsedSensor;
    private readonly ISensor? _gpuMemoryTotalSensor;

    /// <summary>
    /// Only the hardware that our sensors belong to. Refreshing just these each second,
    /// instead of everything, keeps the work per refresh as small as possible.
    /// </summary>
    private readonly IHardware[] _hardwareToUpdate;

    /// <summary>
    /// Connects to the hardware and finds all the sensors. Takes a second or two.
    /// </summary>
    // A constructor: runs once when someone writes "new HardwareMonitor()".
    // It has the same name as the class and no return type.
    public HardwareMonitor()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMotherboardEnabled = true,
        };
        _computer.Open();

        // Some sensors (like the motherboard fans) only appear after the first refresh,
        // so refresh everything once before looking for them.
        foreach (IHardware hardware in _computer.Hardware)
        {
            UpdateWithChildren(hardware);
        }

        _cpuUtilizationSensor = FindSensor(KnownSensors.CpuUtilization);
        _cpuTemperatureSensor = FindSensor(KnownSensors.CpuTemperature);
        _cpuClockSensor = FindSensor(KnownSensors.CpuClock);
        _cpuPowerSensor = FindSensor(KnownSensors.CpuPower);
        _cpuFanSensor = FindSensor(KnownSensors.CpuFan);
        _gpuUtilizationSensor = FindSensor(KnownSensors.GpuUtilization);
        _gpuTemperatureSensor = FindSensor(KnownSensors.GpuTemperature);
        _gpuClockSensor = FindSensor(KnownSensors.GpuClock);
        _gpuVoltageSensor = FindSensor(KnownSensors.GpuVoltage);
        _gpuPowerSensor = FindSensor(KnownSensors.GpuPower);
        _gpuFanSensor = FindSensor(KnownSensors.GpuFan);
        _gpuMemoryUsedSensor = FindSensor(KnownSensors.GpuMemoryUsed);
        _gpuMemoryTotalSensor = FindSensor(KnownSensors.GpuMemoryTotal);

        // Work out which hardware to refresh each second:
        //   1. put all our sensors in an array (some may be null),
        //   2. OfType<ISensor>() drops the nulls,
        //   3. Select(...) takes the hardware each sensor belongs to,
        //   4. Distinct() removes duplicates (e.g. ten sensors on the same GPU = one GPU),
        //   5. ToArray() stores the result.
        ISensor?[] allSensors =
        [
            _cpuUtilizationSensor, _cpuTemperatureSensor, _cpuClockSensor, _cpuPowerSensor, _cpuFanSensor,
            _gpuUtilizationSensor, _gpuTemperatureSensor, _gpuClockSensor, _gpuVoltageSensor, _gpuPowerSensor, _gpuFanSensor,
            _gpuMemoryUsedSensor, _gpuMemoryTotalSensor,
        ];
        _hardwareToUpdate = allSensors
            .OfType<ISensor>()
            .Select(sensor => sensor.Hardware)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Refreshes the sensors and returns their current values.
    /// </summary>
    public MetricsSnapshot ReadMetrics()
    {
        foreach (IHardware hardware in _hardwareToUpdate)
        {
            hardware.Update();
        }

        // RAM comes straight from Windows, not from LibreHardwareMonitor.
        // "var (a, b) = ..." unpacks the two values of the returned tuple.
        var (ramUsedGigabytes, ramTotalGigabytes) = SystemMemory.ReadUsage();

        return new MetricsSnapshot(
            CpuUtilizationPercent: _cpuUtilizationSensor?.Value,
            CpuTemperatureCelsius: _cpuTemperatureSensor?.Value,
            CpuClockMhz: _cpuClockSensor?.Value,
            CpuPowerWatts: _cpuPowerSensor?.Value,
            CpuFanRpm: _cpuFanSensor?.Value,
            RamUsedGigabytes: ramUsedGigabytes,
            RamTotalGigabytes: ramTotalGigabytes,
            GpuUtilizationPercent: _gpuUtilizationSensor?.Value,
            GpuTemperatureCelsius: _gpuTemperatureSensor?.Value,
            GpuClockMhz: _gpuClockSensor?.Value,
            GpuVoltageVolts: _gpuVoltageSensor?.Value,
            GpuPowerWatts: _gpuPowerSensor?.Value,
            GpuFanRpm: _gpuFanSensor?.Value,
            GpuMemoryUsedMegabytes: _gpuMemoryUsedSensor?.Value,
            GpuMemoryTotalMegabytes: _gpuMemoryTotalSensor?.Value);
    }

    /// <summary>
    /// Releases the hardware and the driver connection. Call when the app closes.
    /// </summary>
    public void Dispose()
    {
        _computer.Close();
    }

    /// <summary>
    /// Finds the sensor matching the given ID, including on sub-hardware
    /// like the motherboard chip. Returns null if this PC doesn't have it.
    /// </summary>
    /// <param name="sensorId">Which sensor to look for.</param>
    // Same LINQ chain as in SensorLive: flatten hardware + sub-hardware, keep the right kind,
    // collect their sensors, take the first one with the right type and name.
    private ISensor? FindSensor(SensorId sensorId)
    {
        return _computer.Hardware
            .SelectMany(hardware => hardware.SubHardware.Prepend(hardware))
            .Where(hardware => hardware.HardwareType == sensorId.HardwareType)
            .SelectMany(hardware => hardware.Sensors)
            .FirstOrDefault(sensor => sensor.SensorType == sensorId.SensorType && sensor.Name == sensorId.Name);
    }

    /// <summary>
    /// Refreshes one piece of hardware, then all of its sub-hardware (recursively).
    /// </summary>
    /// <param name="hardware">The hardware to refresh.</param>
    private static void UpdateWithChildren(IHardware hardware)
    {
        hardware.Update();

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            UpdateWithChildren(subHardware);
        }
    }
}
