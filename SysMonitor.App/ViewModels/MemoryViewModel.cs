using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty
using SysMonitor.App.Formatting;            // ValueFormatter
using SysMonitor.Core.Hardware;             // MetricsSnapshot

namespace SysMonitor.App.ViewModels;

/// <summary>
/// What the Memory section shows, as display text.
/// </summary>
internal sealed partial class MemoryViewModel : ObservableObject
{
    /// <summary>RAM usage, e.g. "11.5 / 15.9 GB".</summary>
    [ObservableProperty] private string _ram = ValueFormatter.MissingValue;

    /// <summary>
    /// Shows new sensor values.
    /// </summary>
    /// <param name="metrics">The latest sensor values.</param>
    public void Update(MetricsSnapshot metrics)
    {
        Ram = ValueFormatter.FormatUsage(metrics.RamUsedGigabytes, metrics.RamTotalGigabytes);
    }
}
