namespace SysMonitor.App.Settings;

/// <summary>
/// Everything Datchik remembers between runs. Saved as JSON by <see cref="SettingsStore"/>.
/// The values written here are the defaults, used on first run or if the file can't be read.
/// </summary>
// A plain class with properties. The JSON library creates one with "new AppSettings()",
// then fills in each property from the file, so it needs public "get; set;" properties.
internal sealed class AppSettings
{
    /// <summary>True = extended mode (all metrics); false = basic mode.</summary>
    // "{ get; set; }" = an auto-property: a value other code can read and change,
    // stored in a hidden field that C# creates for you.
    public bool IsExtendedMode { get; set; } = false;

    /// <summary>
    /// The window's last left edge, in WPF units. null = never saved, so let Windows choose.
    /// </summary>
    public double? WindowLeft { get; set; }

    /// <summary>
    /// The window's last top edge, in WPF units. null = never saved, so let Windows choose.
    /// </summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// Which motherboard fan is the CPU fan, by the name the motherboard chip reports (e.g. "Fan #1").
    /// Chosen by right-clicking the CPU Fan row.
    /// </summary>
    public string CpuFanSensorName { get; set; } = "Fan #1";
}
