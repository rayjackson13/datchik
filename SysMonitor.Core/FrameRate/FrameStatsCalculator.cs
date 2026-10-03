namespace SysMonitor.Core.FrameRate;

/// <summary>
/// Turns a list of frame timestamps into FPS and 1% low numbers.
/// </summary>
internal static class FrameStatsCalculator
{
    /// <summary>
    /// Calculates current FPS and 1% low from frame timestamps.
    /// </summary>
    /// <param name="timestamps">Frame timestamps in milliseconds, oldest first. Must contain at least 2.</param>
    /// <param name="nowMs">The timestamp to treat as "now", in milliseconds.</param>
    /// <returns>Current FPS (frames in the last second) and 1% low FPS.</returns>
    public static (int CurrentFps, double OnePercentLowFps) Calculate(double[] timestamps, double nowMs)
    {
        // FPS: count the frames within the last FpsWindowMs.
        int currentFps = timestamps.Count(timestamp => timestamp >= nowMs - FrameRateSettings.FpsWindowMs);

        // Frame durations: the time between each frame and the one before it (N timestamps = N-1 gaps).
        double[] frameDurationsMs = new double[timestamps.Length - 1];
        for (int frameIndex = 1; frameIndex < timestamps.Length; frameIndex++)
        {
            frameDurationsMs[frameIndex - 1] = timestamps[frameIndex] - timestamps[frameIndex - 1];
        }

        // Sort from fastest frame (shortest duration) to slowest.
        Array.Sort(frameDurationsMs);

        // The frame duration that 99% of frames beat. "- 1" turns a count into an array position.
        int percentileIndex = (int)Math.Ceiling(frameDurationsMs.Length * FrameRateSettings.OnePercentLowPercentile) - 1;

        // Milliseconds per frame → frames per second.
        double onePercentLowFps = 1000 / frameDurationsMs[percentileIndex];

        return (currentFps, onePercentLowFps);
    }
}
