// ============================================================================
// FpsSpike: a throwaway experiment that measures FPS and 1% low for a game.
//
// HOW IT WORKS
// Every time a game finishes a frame, it calls a "Present" function to hand
// the frame to Windows. Windows can report those calls through ETW (Event
// Tracing for Windows), its built-in event logging system. We listen to those
// events, record when each frame happened, and calculate FPS and 1% low from
// the timestamps.
//
// HOW TO READ THE COMMENTS
//   ///  "documentation comments": shown in the popup when you hover over a name
//   //   regular comments: only visible here in the code
// ============================================================================

// "using" lines import namespaces (groups of ready-made classes) so we can use
// their classes by short name, e.g. "Process" instead of "System.Diagnostics.Process".
using System.Diagnostics;                     // Process: look up a process's name by its ID
using System.Runtime.InteropServices;         // DllImport: call functions inside Windows' own DLLs
using Microsoft.Diagnostics.Tracing;          // TraceEvent: represents one ETW event
using Microsoft.Diagnostics.Tracing.Session;  // TraceEventSession: a live ETW listening session

// ---------------------------------------------------------------------------
// SHARED STATE
// These variables are used by two threads at the same time:
//   - the background thread, which receives frame events from Windows
//   - the main thread, which calculates and prints the numbers every second
// Every read or write of them must happen inside  lock (frameDataLock) { ... }
// so the two threads never touch the data at the same moment.
// ---------------------------------------------------------------------------

// For every process that renders frames: the timestamps (in milliseconds) of its recent frames.
//   Dictionary<int, Queue<double>>  is a lookup table:
//     key   = process ID (int, a whole number)
//     value = Queue<double>, a first-in-first-out list of numbers with decimals.
//   A queue fits well: new frames are added at the back, old ones removed from the front.
// "var" lets the compiler work out the type from the right-hand side; it is still strictly typed.
var frameTimestampsByProcess = new Dictionary<int, Queue<double>>();

// An object whose only job is to be "locked". While one thread is inside a
// lock (frameDataLock) { ... } block, the other thread waits at its own lock statement.
var frameDataLock = new object();

// Timestamp of the newest frame received from any process, in milliseconds.
// We use it as "now". Windows delivers events in batches roughly once a second,
// so the real clock would be ahead of the data; the newest event's timestamp is not.
double newestTimestampMs = 0;

// Process ID of the game we're currently showing.
// "int?" means "an int OR null". null = no game found yet.
int? gameProcessId = null;

// ---------------------------------------------------------------------------
// START LISTENING TO WINDOWS
// ---------------------------------------------------------------------------

// Create a live ETW session. The name is how Windows identifies it; any unique name works.
//   "using var"          = shut the session down automatically when the program ends.
//   StopOnDispose = true = make sure Windows really stops it too; otherwise the session
//                          could keep running in the background after we exit.
//   { StopOnDispose = true } right after "new ..." sets a property while creating the object.
using var traceSession = new TraceEventSession("SysMonitorFpsSpike") { StopOnDispose = true };

// When you press Ctrl+C, shut the session down cleanly.
//   "+="           = add a function to the list of functions called when this event happens.
//   "(_, _) => ..." = a lambda, a small unnamed function. The two "_" are parameters
//                     Windows passes in that we don't need.
Console.CancelKeyPress += (_, _) => traceSession.Dispose();

// Ask Windows to send us events from the two graphics providers we care about.
traceSession.EnableProvider(PresentEvents.DxgiProviderId); // DirectX 10, 11, 12
traceSession.EnableProvider(PresentEvents.D3d9ProviderId); // DirectX 9

// This lambda runs for EVERY event the session receives, on the background thread.
// "traceEvent" is the event that just arrived.
traceSession.Source.AllEvents += traceEvent =>
{
    // Ignore everything that isn't "a frame is being presented".
    if (!PresentEvents.IsPresentEvent(traceEvent)) return;

    lock (frameDataLock)
    {
        // Find this process's timestamp queue. If it's the process's first frame, create one.
        //   TryGetValue returns true if the key exists, and hands the value back
        //   through the "out var frameTimestamps" parameter.
        //   "!" means "not", so this block runs when the process is NOT in the dictionary yet.
        if (!frameTimestampsByProcess.TryGetValue(traceEvent.ProcessID, out var frameTimestamps))
        {
            frameTimestamps = new Queue<double>();
            frameTimestampsByProcess[traceEvent.ProcessID] = frameTimestamps;
        }

        // Record when this frame happened (milliseconds since the session started).
        frameTimestamps.Enqueue(traceEvent.TimeStampRelativeMSec);
        newestTimestampMs = Math.Max(newestTimestampMs, traceEvent.TimeStampRelativeMSec);
    }
};

// Start delivering events to the handler above. Process() never returns while the
// session is alive, so Task.Run puts it on a background thread; otherwise the program
// would get stuck on this line and never reach the loop below.
Task.Run(() => traceSession.Source.Process());

// ---------------------------------------------------------------------------
// MAIN LOOP: once a second, pick the game and print its numbers
// ---------------------------------------------------------------------------

while (true) // "true" never changes, so this loops until you press Ctrl+C
{
    Thread.Sleep(1000); // wait 1000 ms = 1 second
    Console.Clear();

    lock (frameDataLock)
    {
        // Step 1: trim old frames, and forget processes that stopped rendering.
        //   .ToList() makes a copy to loop over, because C# doesn't allow removing
        //   items from a dictionary while looping over that same dictionary.
        //   "var (processId, frameTimestamps)" splits each key-value pair into two variables.
        foreach (var (processId, frameTimestamps) in frameTimestampsByProcess.ToList())
        {
            // Peek() = look at the oldest timestamp without removing it.
            // Remove frames that are older than the window the 1% low looks at.
            while (frameTimestamps.Count > 0 &&
                   frameTimestamps.Peek() < newestTimestampMs - FpsSpikeSettings.OnePercentLowWindowMs)
            {
                frameTimestamps.Dequeue(); // remove the oldest timestamp
            }

            // We need at least 2 frames to measure the time between them.
            if (frameTimestamps.Count < 2)
            {
                frameTimestampsByProcess.Remove(processId);
            }
        }

        // Step 2: decide which process is the game.
        // Follow the foreground window, but only if it renders frames and isn't on the
        // ignore list. If it doesn't qualify (e.g. you clicked on the terminal), keep the
        // previous game. That's what makes this work with the game on another monitor.
        int foregroundProcessId = ProcessHelpers.GetForegroundProcessId();
        if (frameTimestampsByProcess.ContainsKey(foregroundProcessId) &&
            !FpsSpikeSettings.IgnoredProcessNames.Contains(ProcessHelpers.GetProcessName(foregroundProcessId)))
        {
            gameProcessId = foregroundProcessId;
        }

        // Step 3: print the game's numbers.
        //   "gameProcessId is int processIdToShow" checks that the int? actually holds a
        //   value and, if so, copies it into a regular int named processIdToShow.
        //   "&&" = "and": both conditions must be true.
        if (gameProcessId is int processIdToShow &&
            frameTimestampsByProcess.TryGetValue(processIdToShow, out var gameFrameTimestamps))
        {
            // The function returns two values at once (a "tuple");
            // "var (a, b) = ..." unpacks them into two variables.
            var (currentFps, onePercentLowFps) =
                FrameStatsCalculator.Calculate(gameFrameTimestamps.ToArray(), newestTimestampMs);

            // $"..." is an interpolated string: values in { } are inserted into the text.
            // {onePercentLowFps:0} = format with no decimal places.
            Console.WriteLine($"Game:    {ProcessHelpers.GetProcessName(processIdToShow)}");
            Console.WriteLine($"FPS:     {currentFps}");
            Console.WriteLine($"1% low:  {onePercentLowFps:0}");
        }
        else
        {
            Console.WriteLine("Waiting for a game...");
        }
    }
}

// ============================================================================
// CLASSES
// In a file with top-level code (like the code above), class declarations must
// come after all of it. "static class" = a class you never create objects of;
// it just groups related constants and functions under one name.
// ============================================================================

/// <summary>
/// Tuning values for this spike: how FPS and 1% low are calculated, and which processes to ignore.
/// </summary>
static class FpsSpikeSettings
{
    /// <summary>FPS = the number of frames presented within this window, in milliseconds (1000 = 1 second).</summary>
    public const double FpsWindowMs = 1_000; // "_" in numbers is just a visual separator, like a space

    /// <summary>How far back the 1% low looks, in milliseconds. Longer = steadier but slower to react.</summary>
    public const double OnePercentLowWindowMs = 30_000;

    /// <summary>
    /// Which frame time counts as the "1% low": 0.99 = the frame time that 99% of frames are faster than.
    /// </summary>
    public const double OnePercentLowPercentile = 0.99;

    /// <summary>
    /// Processes that render frames but are never the game, so they're never picked as the target.
    /// Names are compared without regard to upper/lower case.
    /// </summary>
    // "static readonly" instead of "const": const only works for simple values like numbers and text.
    // HashSet = a collection built for fast "is this item in here?" checks.
    // "new(...)" without a type name: the type is taken from the declaration on the left.
    public static readonly HashSet<string> IgnoredProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsTerminal", // the terminal running this spike
        "dwm",             // Desktop Window Manager, draws the Windows desktop itself
        "explorer",        // File Explorer and the taskbar
    };
}

/// <summary>
/// Identifies the Windows events that mean "a program is presenting a frame".
/// </summary>
static class PresentEvents
{
    /// <summary>ID of the "Microsoft-Windows-DXGI" event provider, used by DirectX 10, 11 and 12.</summary>
    public static readonly Guid DxgiProviderId = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

    /// <summary>ID of the "Microsoft-Windows-D3D9" event provider, used by older DirectX 9 games.</summary>
    public static readonly Guid D3d9ProviderId = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    /// <summary>Event number that DXGI sends when a frame starts being presented.</summary>
    public const int DxgiPresentStartEventId = 42;

    /// <summary>Event number that D3D9 sends when a frame starts being presented.</summary>
    public const int D3d9PresentStartEventId = 1;

    /// <summary>
    /// Returns true if the event means "a frame is being presented", from either provider.
    /// </summary>
    /// <param name="traceEvent">An event received from the ETW session.</param>
    // "=> ..." after a function signature means "this function just returns this expression".
    // "(int)traceEvent.ID" converts the event ID to a plain int so we can compare it with our numbers.
    // "||" = "or".
    public static bool IsPresentEvent(TraceEvent traceEvent) =>
        (traceEvent.ProviderGuid == DxgiProviderId && (int)traceEvent.ID == DxgiPresentStartEventId) ||
        (traceEvent.ProviderGuid == D3d9ProviderId && (int)traceEvent.ID == D3d9PresentStartEventId);
}

/// <summary>
/// Turns a list of frame timestamps into FPS and 1% low numbers.
/// </summary>
static class FrameStatsCalculator
{
    /// <summary>
    /// Calculates current FPS and 1% low from frame timestamps.
    /// </summary>
    /// <param name="timestamps">Frame timestamps in milliseconds, oldest first. Must contain at least 2.</param>
    /// <param name="nowMs">The timestamp to treat as "now", in milliseconds.</param>
    /// <returns>Current FPS (frames in the last second) and 1% low FPS.</returns>
    // "(int CurrentFps, double OnePercentLowFps)" as the return type = this function returns
    // two named values at once (a tuple), instead of needing a whole class for them.
    public static (int CurrentFps, double OnePercentLowFps) Calculate(double[] timestamps, double nowMs)
    {
        // FPS: count the frames whose timestamp falls within the last FpsWindowMs.
        //   .Count(condition) counts the items for which the condition is true.
        //   "timestamp => ..." is a lambda run once per item.
        int currentFps = timestamps.Count(timestamp => timestamp >= nowMs - FpsSpikeSettings.FpsWindowMs);

        // Frame durations: the time between each frame and the one before it.
        // N timestamps give N-1 gaps, hence the "- 1".
        double[] frameDurationsMs = new double[timestamps.Length - 1];
        for (int frameIndex = 1; frameIndex < timestamps.Length; frameIndex++)
        {
            frameDurationsMs[frameIndex - 1] = timestamps[frameIndex] - timestamps[frameIndex - 1];
        }

        // Sort from fastest frame (shortest duration) to slowest (longest).
        Array.Sort(frameDurationsMs);

        // Find the frame duration at the 99th percentile: 99% of frames were faster than this one.
        //   Math.Ceiling rounds up; "- 1" turns a count into an array position (arrays start at 0).
        int percentileIndex = (int)Math.Ceiling(frameDurationsMs.Length * FpsSpikeSettings.OnePercentLowPercentile) - 1;

        // Convert that frame duration to FPS: 1000 ms divided by milliseconds per frame.
        double onePercentLowFps = 1000 / frameDurationsMs[percentileIndex];

        return (currentFps, onePercentLowFps);
    }
}

/// <summary>
/// Small helpers for finding out about running processes.
/// </summary>
static class ProcessHelpers
{
    /// <summary>
    /// Returns the name of a process (e.g. "witcher3"), or its ID as text if the name can't be read.
    /// </summary>
    /// <param name="processId">The process ID to look up.</param>
    public static string GetProcessName(int processId)
    {
        // try/catch: if anything inside "try" fails (e.g. the process already exited),
        // run the "catch" part instead of crashing.
        try
        {
            // "using var" releases the Process object's Windows resources when we're done with it.
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return processId.ToString();
        }
    }

    /// <summary>
    /// Returns the ID of the process that owns the window you're currently using.
    /// </summary>
    public static int GetForegroundProcessId()
    {
        // Ask Windows which window is in the foreground. IntPtr is a "handle":
        // a number Windows uses to refer to the window.
        IntPtr foregroundWindow = NativeMethods.GetForegroundWindow();

        // Ask Windows which process owns that window. The answer comes back through "out".
        NativeMethods.GetWindowThreadProcessId(foregroundWindow, out uint processId);

        // Windows gives an unsigned int (uint, never negative); the rest of our code uses int.
        return (int)processId;
    }
}

/// <summary>
/// Functions that live inside Windows itself (in user32.dll), made callable from C#.
/// This technique is called P/Invoke.
/// </summary>
static class NativeMethods
{
    /// <summary>Windows API: returns a handle to the window currently in the foreground.</summary>
    // [DllImport("user32.dll")] tells C# which Windows DLL contains this function.
    // "extern" = the function's code isn't here; it lives in that DLL.
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>Windows API: finds which process owns the given window.</summary>
    /// <param name="windowHandle">The window to ask about.</param>
    /// <param name="processId">Receives the ID of the process that owns the window.</param>
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}