using LibreHardwareMonitor.Hardware; // Computer, IHardware, ISensor, HardwareType, SensorType

namespace SysMonitor.Core.Hardware;

/// <summary>
/// Reads the CPU, GPU and motherboard sensors the app displays. Create one when the app starts,
/// call <see cref="ReadMetrics"/> once per refresh, and dispose it when the app closes.
/// Requires administrator rights for CPU and motherboard sensors.
/// </summary>
public sealed class HardwareMonitor : IDisposable
{
    /// <summary>LibreHardwareMonitor's view of this PC.</summary>
    private readonly Computer _computer;

    // Each single sensor we display, found once at startup. "ISensor?" = null if this PC doesn't have it.
    private readonly ISensor? _cpuUtilizationSensor;
    private readonly ISensor? _cpuTemperatureSensor;
    private readonly ISensor? _cpuClockSensor;
    private readonly ISensor? _cpuPowerSensor;
    private readonly ISensor? _gpuUtilizationSensor;
    private readonly ISensor? _gpuTemperatureSensor;
    private readonly ISensor? _gpuClockSensor;
    private readonly ISensor? _gpuVoltageSensor;
    private readonly ISensor? _gpuPowerSensor;
    private readonly ISensor? _gpuFanSensor;
    private readonly ISensor? _gpuMemoryUsedSensor;
    private readonly ISensor? _gpuMemoryTotalSensor;

    /// <summary>
    /// Every fan sensor on the motherboard's monitoring chip (SuperIO). Empty if the chip
    /// isn't supported or the driver isn't available.
    /// </summary>
    private readonly ISensor[] _motherboardFanSensors;

    /// <summary>
    /// Only the hardware that our sensors belong to. Refreshing just these each second,
    /// instead of everything, keeps the work per refresh as small as possible.
    /// </summary>
    private readonly IHardware[] _hardwareToUpdate;

    /// <summary>
    /// Connects to the hardware and finds all the sensors. Takes a second or two.
    /// </summary>
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
        _gpuUtilizationSensor = FindSensor(KnownSensors.GpuUtilization);
        _gpuTemperatureSensor = FindSensor(KnownSensors.GpuTemperature);
        _gpuClockSensor = FindSensor(KnownSensors.GpuClock);
        _gpuVoltageSensor = FindSensor(KnownSensors.GpuVoltage);
        _gpuPowerSensor = FindSensor(KnownSensors.GpuPower);
        _gpuFanSensor = FindSensor(KnownSensors.GpuFan);
        _gpuMemoryUsedSensor = FindSensor(KnownSensors.GpuMemoryUsed);
        _gpuMemoryTotalSensor = FindSensor(KnownSensors.GpuMemoryTotal);

        _motherboardFanSensors = FindMotherboardFanSensors();

        // Work out which hardware to refresh each second:
        //   1. put all the single sensors in an array (some may be null),
        //   2. OfType<ISensor>() drops the nulls,
        //   3. Concat(...) adds the motherboard fan sensors to the list,
        //   4. Select(...) takes the hardware each sensor belongs to,
        //   5. Distinct() removes duplicates (ten sensors on the same GPU = one GPU),
        //   6. ToArray() stores the result.
        ISensor?[] singleSensors =
        [
            _cpuUtilizationSensor, _cpuTemperatureSensor, _cpuClockSensor, _cpuPowerSensor,
            _gpuUtilizationSensor, _gpuTemperatureSensor, _gpuClockSensor, _gpuVoltageSensor,
            _gpuPowerSensor, _gpuFanSensor, _gpuMemoryUsedSensor, _gpuMemoryTotalSensor,
        ];
        _hardwareToUpdate = singleSensors
            .OfType<ISensor>()
            .Concat(_motherboardFanSensors)
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
        var (ramUsedGigabytes, ramTotalGigabytes) = SystemMemory.ReadUsage();

        // Turn each fan sensor into a FanSpeed (its name plus current speed).
        FanSpeed[] motherboardFans = _motherboardFanSensors
            .Select(sensor => new FanSpeed(sensor.Name, sensor.Value))
            .ToArray();

        return new MetricsSnapshot(
            CpuUtilizationPercent: _cpuUtilizationSensor?.Value,
            CpuTemperatureCelsius: _cpuTemperatureSensor?.Value,
            CpuClockMhz: _cpuClockSensor?.Value,
            CpuPowerWatts: _cpuPowerSensor?.Value,
            RamUsedGigabytes: ramUsedGigabytes,
            RamTotalGigabytes: ramTotalGigabytes,
            GpuUtilizationPercent: _gpuUtilizationSensor?.Value,
            GpuTemperatureCelsius: _gpuTemperatureSensor?.Value,
            GpuClockMhz: _gpuClockSensor?.Value,
            GpuVoltageVolts: _gpuVoltageSensor?.Value,
            GpuPowerWatts: _gpuPowerSensor?.Value,
            GpuFanRpm: _gpuFanSensor?.Value,
            GpuMemoryUsedMegabytes: _gpuMemoryUsedSensor?.Value,
            GpuMemoryTotalMegabytes: _gpuMemoryTotalSensor?.Value,
            MotherboardFans: motherboardFans);
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
    private ISensor? FindSensor(SensorId sensorId)
    {
        return AllHardware()
            .Where(hardware => hardware.HardwareType == sensorId.HardwareType)
            .SelectMany(hardware => hardware.Sensors)
            .FirstOrDefault(sensor => sensor.SensorType == sensorId.SensorType && sensor.Name == sensorId.Name);
    }

    /// <summary>
    /// Finds every fan sensor on the motherboard's monitoring chip (SuperIO).
    /// </summary>
    private ISensor[] FindMotherboardFanSensors()
    {
        return AllHardware()
            .Where(hardware => hardware.HardwareType == HardwareType.SuperIO)
            .SelectMany(hardware => hardware.Sensors)
            .Where(sensor => sensor.SensorType == SensorType.Fan)
            .ToArray();
    }

    /// <summary>
    /// All hardware plus its sub-hardware as one flat list, so searches also find
    /// sensors on the motherboard chip, which is sub-hardware of the motherboard.
    /// </summary>
    // IEnumerable<T> = "something you can loop over". LINQ methods like Where and Select work on it.
    private IEnumerable<IHardware> AllHardware()
    {
        return _computer.Hardware.SelectMany(hardware => hardware.SubHardware.Prepend(hardware));
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
