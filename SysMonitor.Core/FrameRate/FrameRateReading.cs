namespace SysMonitor.Core.FrameRate;

/// <summary>
/// The frame rate of the game being tracked, at one moment.
/// </summary>
/// <param name="GameName">The game's process name, e.g. "witcher3".</param>
/// <param name="CurrentFps">Frames presented in the last second.</param>
/// <param name="OnePercentLowFps">The 1% low, in FPS: how low the frame rate drops during stutters.</param>
public record FrameRateReading(string GameName, int CurrentFps, double OnePercentLowFps);
