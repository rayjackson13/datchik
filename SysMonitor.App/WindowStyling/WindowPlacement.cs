using System.Windows;          // Window, WindowState, WindowStartupLocation, SystemParameters, Rect
using SysMonitor.App.Settings; // AppSettings, SettingsStore

namespace SysMonitor.App.WindowStyling;

/// <summary>
/// Saves and restores a window's position between runs.
/// </summary>
internal static class WindowPlacement
{
    /// <summary>
    /// How much of the window (in WPF units) must fit on the screens for a saved position to be
    /// used. Stops the window from opening off-screen, e.g. after unplugging a monitor.
    /// </summary>
    private const double MinimumVisibleWindowPart = 50;

    /// <summary>
    /// Moves the window to its saved position, if there is one and it's still on a connected screen.
    /// Call before the window is shown.
    /// </summary>
    /// <param name="window">The window to move.</param>
    /// <param name="settings">The settings holding the saved position.</param>
    public static void Restore(Window window, AppSettings settings)
    {
        if (settings.WindowLeft is not double left || settings.WindowTop is not double top)
        {
            return;
        }

        // The "virtual screen" is one big rectangle covering all connected monitors.
        bool isOnScreen =
            left >= SystemParameters.VirtualScreenLeft &&
            top >= SystemParameters.VirtualScreenTop &&
            left <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - MinimumVisibleWindowPart &&
            top <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - MinimumVisibleWindowPart;

        if (!isOnScreen)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
    }

    /// <summary>
    /// Saves the window's current position. If it's minimized, saves where it will return to
    /// instead (a minimized window sits at a far off-screen position).
    /// </summary>
    /// <param name="window">The window whose position to save.</param>
    /// <param name="settings">The settings to store the position in.</param>
    public static void Save(Window window, AppSettings settings)
    {
        Rect normalBounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.Width, window.Height)
            : window.RestoreBounds;

        settings.WindowLeft = normalBounds.Left;
        settings.WindowTop = normalBounds.Top;
        SettingsStore.Save(settings);
    }
}
