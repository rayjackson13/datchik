using System.Runtime.InteropServices; // DllImport, StructLayout, Marshal

namespace SysMonitor.Core.Hardware;

/// <summary>
/// Reads RAM usage directly from Windows. The same numbers Task Manager shows.
/// </summary>
public static class SystemMemory
{
    /// <summary>Bytes in one gigabyte (1024 × 1024 × 1024), the way Windows counts it.</summary>
    private const double BytesPerGigabyte = 1024.0 * 1024.0 * 1024.0;

    /// <summary>
    /// Returns how much RAM is in use and how much is installed, in gigabytes.
    /// Both values are null if Windows couldn't be asked.
    /// </summary>
    // Returns a tuple of two values. "double?" = a decimal number or null.
    public static (double? UsedGigabytes, double? TotalGigabytes) ReadUsage()
    {
        // Windows requires us to fill in the struct's size before calling,
        // so it knows which version of the struct we're using.
        var memoryStatus = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>(),
        };

        // "ref" = pass the struct itself so Windows can fill in its fields
        // (normally a struct would be copied, and Windows would fill in the copy).
        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            return (null, null);
        }

        // ulong = an unsigned 64-bit whole number: big enough to count bytes of RAM.
        ulong usedBytes = memoryStatus.TotalPhysicalBytes - memoryStatus.AvailablePhysicalBytes;

        return (usedBytes / BytesPerGigabyte, memoryStatus.TotalPhysicalBytes / BytesPerGigabyte);
    }

    /// <summary>
    /// The data structure Windows fills in with memory information.
    /// The field order and types must match Windows' definition exactly,
    /// because Windows writes into it byte by byte.
    /// </summary>
    // "struct" = like a class, but a plain block of data, copied by value.
    // StructLayout(Sequential) = keep the fields in memory in exactly this order.
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;                    // size of this struct, set by us
        public uint MemoryLoadPercent;         // percent of RAM in use
        public ulong TotalPhysicalBytes;       // installed RAM
        public ulong AvailablePhysicalBytes;   // RAM free for programs to use
        public ulong TotalPageFileBytes;       // the rest isn't used by us, but must be
        public ulong AvailablePageFileBytes;   // present so the layout matches Windows
        public ulong TotalVirtualBytes;
        public ulong AvailableVirtualBytes;
        public ulong AvailableExtendedVirtualBytes;
    }

    /// <summary>Windows API: fills in a <see cref="MemoryStatusEx"/>. Returns false on failure.</summary>
    // [return: MarshalAs(...Bool)] = Windows returns a 4-byte "BOOL"; convert it to a C# bool.
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx memoryStatus);
}
