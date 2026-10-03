namespace SysMonitor.Core.FrameRate;

/// <summary>
/// Tuning values for FPS tracking: how the numbers are calculated and which processes are never the game.
/// </summary>
internal static class FrameRateSettings
{
    /// <summary>
    /// The name of our ETW session. Windows identifies sessions by name, so it must be unique
    /// to this app; if a previous run crashed and left one behind, it gets replaced.
    /// </summary>
    public const string TraceSessionName = "DatchikFrameTracker";

    /// <summary>FPS = the number of frames presented within this window, in milliseconds.</summary>
    public const double FpsWindowMs = 1_000;

    /// <summary>How far back the 1% low looks, in milliseconds. Longer = steadier but slower to react.</summary>
    public const double OnePercentLowWindowMs = 30_000;

    /// <summary>Which frame time counts as the "1% low": 0.99 = the frame time 99% of frames are faster than.</summary>
    public const double OnePercentLowPercentile = 0.99;

    /// <summary>
    /// If the game hasn't presented a frame for this long, in milliseconds, it's treated as stopped
    /// (closed, minimized, loading) and no numbers are shown.
    /// </summary>
    public const double GameTimeoutMs = 3_000;

    /// <summary>
    /// Processes that render frames but are never the game. Compared without regard to upper/lower case.
    /// Datchik itself is excluded separately, by process ID.
    /// </summary>
    public static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm",             // Desktop Window Manager, draws the Windows desktop itself
        "explorer",        // File Explorer and the taskbar
        "WindowsTerminal", // the terminal
    };
}
