using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty
using SysMonitor.App.Formatting;            // ValueFormatter
using SysMonitor.Core.Hardware;             // MetricsSnapshot

namespace SysMonitor.App.ViewModels;

/// <summary>
/// What the GPU section shows, as display text.
/// </summary>
internal sealed partial class GpuViewModel : ObservableObject
{
    [ObservableProperty] private string _utilization = ValueFormatter.MissingValue;
    [ObservableProperty] private string _temperature = ValueFormatter.MissingValue;
    [ObservableProperty] private string _clock = ValueFormatter.MissingValue;
    [ObservableProperty] private string _voltage = ValueFormatter.MissingValue;
    [ObservableProperty] private string _power = ValueFormatter.MissingValue;
    [ObservableProperty] private string _fan = ValueFormatter.MissingValue;

    /// <summary>Video memory usage, e.g. "3.0 / 12.0 GB".</summary>
    [ObservableProperty] private string _memory = ValueFormatter.MissingValue;

    /// <summary>
    /// Shows new sensor values.
    /// </summary>
    /// <param name="metrics">The latest sensor values.</param>
    public void Update(MetricsSnapshot metrics)
    {
        Utilization = ValueFormatter.FormatValue(metrics.GpuUtilizationPercent, "%");
        Temperature = ValueFormatter.FormatValue(metrics.GpuTemperatureCelsius, "°C");
        Clock = ValueFormatter.FormatValue(metrics.GpuClockMhz, "MHz");
        Voltage = ValueFormatter.FormatValue(metrics.GpuVoltageVolts, "V", decimalFormat: "0.00");
        Power = ValueFormatter.FormatValue(metrics.GpuPowerWatts, "W");
        Fan = ValueFormatter.FormatValue(metrics.GpuFanRpm, "RPM");

        // VRAM arrives in megabytes; divide by 1024 to show gigabytes, like RAM.
        Memory = ValueFormatter.FormatUsage(
            metrics.GpuMemoryUsedMegabytes / 1024.0,
            metrics.GpuMemoryTotalMegabytes / 1024.0);
    }
}
