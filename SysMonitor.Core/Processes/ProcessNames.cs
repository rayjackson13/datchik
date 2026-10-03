using System.Diagnostics; // Process

namespace SysMonitor.Core.Processes;

/// <summary>
/// Looks up the names of running processes.
/// </summary>
internal static class ProcessNames
{
    /// <summary>
    /// Returns the name of a process (e.g. "witcher3"), or its ID as text if the name can't be read
    /// (for example, because the process already exited).
    /// </summary>
    /// <param name="processId">The process ID to look up.</param>
    public static string Get(int processId)
    {
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
}
