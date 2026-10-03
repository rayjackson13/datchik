using System.Runtime.InteropServices; // DllImport

namespace SysMonitor.Core.Processes;

/// <summary>
/// Finds out which process owns the window you're currently using.
/// </summary>
internal static class ForegroundProcess
{
    /// <summary>
    /// Returns the ID of the process that owns the window currently in the foreground.
    /// </summary>
    public static int GetId()
    {
        // Ask Windows which window is in the foreground (a "handle" = Windows' number for a window),
        // then which process owns that window.
        IntPtr foregroundWindow = GetForegroundWindow();
        GetWindowThreadProcessId(foregroundWindow, out uint processId);

        // Windows gives an unsigned int (uint, never negative); the rest of our code uses int.
        return (int)processId;
    }

    /// <summary>Windows API: returns a handle to the window currently in the foreground.</summary>
    // "private": only this class needs these, so nothing else can call them.
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>Windows API: finds which process owns the given window.</summary>
    /// <param name="windowHandle">The window to ask about.</param>
    /// <param name="processId">Receives the ID of the process that owns the window.</param>
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);
}
