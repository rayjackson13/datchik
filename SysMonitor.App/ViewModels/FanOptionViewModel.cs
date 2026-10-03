using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty

namespace SysMonitor.App.ViewModels;

/// <summary>
/// One entry in the CPU fan menu: a motherboard fan, its current speed, and whether it's the chosen one.
/// </summary>
internal sealed partial class FanOptionViewModel : ObservableObject
{
    /// <summary>Creates a menu entry for the fan with the given name.</summary>
    /// <param name="name">The fan's name as the motherboard chip reports it, e.g. "Fan #2".</param>
    public FanOptionViewModel(string name)
    {
        Name = name;
    }

    /// <summary>The fan's name, e.g. "Fan #2". Never changes, so it's a plain property.</summary>
    public string Name { get; }

    /// <summary>The menu text, e.g. "Fan #2  —  1324 RPM". Updated as the speed changes.</summary>
    [ObservableProperty]
    private string _label = string.Empty;

    /// <summary>True if this is the fan currently shown as the CPU fan (gets a checkmark).</summary>
    [ObservableProperty]
    private bool _isSelected;
}
