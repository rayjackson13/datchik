using Microsoft.Diagnostics.Tracing;         // TraceEvent
using Microsoft.Diagnostics.Tracing.Session;
using SysMonitor.Core.Processes;

namespace SysMonitor.Core.FrameRate;

/// <summary>
/// Measures the FPS and 1% low of the game you're playing. Create one when the app starts,
/// call <see cref="Read"/> once per refresh, and dispose it when the app closes.
/// Requires administrator rights (ETW sessions need them).
/// </summary>
/// <remarks>
/// How it works: Windows reports every "present" (a finished frame handed to Windows) through
/// ETW. We record when each process presents frames, pick the game, and calculate its
/// numbers from the timestamps. Covers DirectX 9–12 games, not Vulkan or OpenGL.
/// </remarks>
public sealed class FrameRateTracker : IDisposable
{
    /// <summary>The live ETW session receiving present events from Windows.</summary>
    private readonly TraceEventSession _traceSession;

    /// <summary>
    /// For every process that renders frames: timestamps (ms) of its recent frames.
    /// Shared between threads; only touch it inside lock (_frameDataLock).
    /// </summary>
    private readonly Dictionary<int, Queue<double>> _frameTimestampsByProcess = new();

    /// <summary>
    /// Guards the shared data above: only one thread at a time can be inside lock (_frameDataLock).
    /// "Lock" is .NET's dedicated lock type (since .NET 9); it does the same job as locking
    /// a plain object, but is faster and makes the intent obvious.
    /// </summary>
    private readonly Lock _frameDataLock = new();

    /// <summary>
    /// Timestamp of the newest frame from any process, in ms. Used as "now", because Windows
    /// delivers events in batches, so the real clock is ahead of the data.
    /// </summary>
    private double _newestTimestampMs;

    /// <summary>Process ID of the game being tracked, or null if none has been found yet.</summary>
    private int? _gameProcessId;

    /// <summary>
    /// Datchik's own process ID. WPF draws with DirectX 9, so our own window sends present
    /// events too; we must never pick ourselves as the game.
    /// </summary>
    private readonly int _ownProcessId = Environment.ProcessId;

    /// <summary>
    /// Starts listening for present events. Returns immediately; events are received in the background.
    /// </summary>
    public FrameRateTracker()
    {
        _traceSession = new TraceEventSession(FrameRateSettings.TraceSessionName) { StopOnDispose = true };
        _traceSession.EnableProvider(PresentEvents.DxgiProviderId);
        _traceSession.EnableProvider(PresentEvents.D3d9ProviderId);

        // Call OnTraceEvent for every event received. Passing a method name instead of a lambda
        // works the same way, and is tidier when the code is more than a line or two.
        _traceSession.Source.AllEvents += OnTraceEvent;

        // Process() never returns while the session is alive, so it runs on a background thread.
        Task.Run(() => _traceSession.Source.Process());
    }

    /// <summary>
    /// Returns the current frame rate of the game, or null if no game is running
    /// (none found yet, or it stopped presenting frames).
    /// </summary>
    public FrameRateReading? Read()
    {
        lock (_frameDataLock)
        {
            RemoveOldFrames();
            UpdateGameProcess();

            // "is not int gameProcessId": if there's no game, stop here.
            // If there is one, its ID is now in gameProcessId.
            if (_gameProcessId is not int gameProcessId ||
                !_frameTimestampsByProcess.TryGetValue(gameProcessId, out var gameFrameTimestamps))
            {
                return null;
            }

            double[] timestamps = gameFrameTimestamps.ToArray();

            // If the game's newest frame is too old, it has stopped rendering: show nothing.
            // timestamps[^1] = the last item in the array ("^1" = first from the end).
            if (_newestTimestampMs - timestamps[^1] > FrameRateSettings.GameTimeoutMs)
            {
                return null;
            }

            var (currentFps, onePercentLowFps) = FrameStatsCalculator.Calculate(timestamps, _newestTimestampMs);
            return new FrameRateReading(ProcessNames.Get(gameProcessId), currentFps, onePercentLowFps);
        }
    }

    /// <summary>
    /// Stops the ETW session. Call when the app closes.
    /// </summary>
    public void Dispose()
    {
        _traceSession.Dispose();
    }

    /// <summary>
    /// Runs on the background thread for every event received: records present events.
    /// </summary>
    /// <param name="traceEvent">The event that just arrived.</param>
    private void OnTraceEvent(TraceEvent traceEvent)
    {
        if (!PresentEvents.IsPresentEvent(traceEvent)) return;

        lock (_frameDataLock)
        {
            if (!_frameTimestampsByProcess.TryGetValue(traceEvent.ProcessID, out var frameTimestamps))
            {
                frameTimestamps = new Queue<double>();
                _frameTimestampsByProcess[traceEvent.ProcessID] = frameTimestamps;
            }

            frameTimestamps.Enqueue(traceEvent.TimeStampRelativeMSec);
            _newestTimestampMs = Math.Max(_newestTimestampMs, traceEvent.TimeStampRelativeMSec);
        }
    }

    /// <summary>
    /// Drops frames older than the 1% low window, and forgets processes that stopped rendering.
    /// Must be called inside lock (_frameDataLock).
    /// </summary>
    private void RemoveOldFrames()
    {
        // .ToList() = loop over a copy, since we may remove items from the dictionary itself.
        foreach (var (processId, frameTimestamps) in _frameTimestampsByProcess.ToList())
        {
            while (frameTimestamps.Count > 0 &&
                   frameTimestamps.Peek() < _newestTimestampMs - FrameRateSettings.OnePercentLowWindowMs)
            {
                frameTimestamps.Dequeue();
            }

            // Fewer than 2 frames = can't measure the time between frames.
            if (frameTimestamps.Count < 2)
            {
                _frameTimestampsByProcess.Remove(processId);
            }
        }
    }

    /// <summary>
    /// Switches to the foreground app as the game, if it renders frames and isn't excluded.
    /// Otherwise keeps the previous game, so clicking on Datchik or another window
    /// doesn't lose track of the game on the other monitor.
    /// Must be called inside lock (_frameDataLock).
    /// </summary>
    private void UpdateGameProcess()
    {
        int foregroundProcessId = ForegroundProcess.GetId();

        // Checked in this order on purpose: "&&" stops at the first false, so the
        // (slower) process name lookup only happens for processes that render frames.
        if (_frameTimestampsByProcess.ContainsKey(foregroundProcessId) &&
            foregroundProcessId != _ownProcessId &&
            !FrameRateSettings.IgnoredProcessNames.Contains(ProcessNames.Get(foregroundProcessId)))
        {
            _gameProcessId = foregroundProcessId;
        }
    }
}
