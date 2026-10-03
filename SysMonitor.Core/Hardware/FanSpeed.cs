namespace SysMonitor.Core.Hardware;

/// <summary>
/// One motherboard fan header and its current speed.
/// </summary>
/// <param name="Name">The fan's name as the motherboard chip reports it, e.g. "Fan #1".</param>
/// <param name="Rpm">Current speed in RPM, or null if there's no reading. 0 usually means nothing is plugged in.</param>
public record FanSpeed(string Name, float? Rpm);
