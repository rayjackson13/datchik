using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty
using SysMonitor.App.Formatting;            // ValueFormatter
using SysMonitor.Core.FrameRate;            // FrameRateReading

namespace SysMonitor.App.ViewModels;

/// <summary>
/// What the Frame Rate section shows: the game, its FPS and its 1% low, as display text.
/// </summary>
// ": ObservableObject" = inherit the toolkit's change-notification machinery.
// "partial" is required: the toolkit generates the other half of this class (the properties).
internal sealed partial class FrameRateViewModel : ObservableObject
{
    // Each [ObservableProperty] field becomes a public property with change notification:
    // _gameName → GameName, _currentFps → CurrentFps, and so on.
    // Setting a property to the value it already has does nothing, so updating every
    // tick only redraws what actually changed.

    /// <summary>The game's process name, e.g. "witcher3".</summary>
    [ObservableProperty]
    private string _gameName = ValueFormatter.MissingValue;

    /// <summary>Current FPS as text, e.g. "144".</summary>
    [ObservableProperty]
    private string _currentFps = ValueFormatter.MissingValue;

    /// <summary>1% low FPS as text, e.g. "97".</summary>
    [ObservableProperty]
    private string _onePercentLow = ValueFormatter.MissingValue;

    /// <summary>
    /// Shows a new frame rate reading, or dashes if no game is running.
    /// </summary>
    /// <param name="frameRate">The latest reading, or null if there's no game.</param>
    public void Update(FrameRateReading? frameRate)
    {
        GameName = frameRate?.GameName ?? ValueFormatter.MissingValue;
        CurrentFps = frameRate is null ? ValueFormatter.MissingValue : frameRate.CurrentFps.ToString();
        OnePercentLow = frameRate is null ? ValueFormatter.MissingValue : frameRate.OnePercentLowFps.ToString("0");
    }
}
