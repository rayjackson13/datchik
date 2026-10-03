using System.Runtime.InteropServices; // DllImport
using System.Windows;                 // Window
using System.Windows.Interop;         // WindowInteropHelper

namespace SysMonitor.App.WindowStyling;

/// <summary>
/// Removes the icon from a window's title bar. WPF has no setting for this, so we change
/// the window's style through the Windows API instead.
/// </summary>
internal static class TitleBarIcon
{
    /// <summary>Which window property GetWindowLong/SetWindowLong work on: the "extended style" flags.</summary>
    private const int GwlExtendedStyle = -20;

    /// <summary>Extended style flag: "dialog frame". Windows draws this frame without an icon.</summary>
    private const int WsExDialogModalFrame = 0x0001;

    // Flags for SetWindowPos. We don't want to move or resize the window,
    // only make Windows redraw its frame with the new style.
    private const uint SwpNoSize = 0x0001;       // keep the current size
    private const uint SwpNoMove = 0x0002;       // keep the current position
    private const uint SwpNoZOrder = 0x0004;     // keep its place in front/behind other windows
    private const uint SwpFrameChanged = 0x0020; // redraw the frame

    /// <summary>
    /// Removes the title bar icon. Call it from the window's SourceInitialized event,
    /// once the window exists on the Windows side.
    /// </summary>
    /// <param name="window">The window to change.</param>
    public static void Remove(Window window)
    {
        IntPtr windowHandle = new WindowInteropHelper(window).Handle;

        // A window's style is a set of on/off flags packed into one number.
        // Read the current flags, switch on "dialog frame", and write them back.
        //   "|" = combine flags: keep all existing ones and add ours.
        int extendedStyle = GetWindowLong(windowHandle, GwlExtendedStyle);
        SetWindowLong(windowHandle, GwlExtendedStyle, extendedStyle | WsExDialogModalFrame);

        // Style changes only show up once Windows redraws the frame; this forces it.
        // The zeros are position and size, which the flags tell Windows to ignore.
        SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    /// <summary>Windows API: reads one of a window's properties (here: its extended style flags).</summary>
    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    /// <summary>Windows API: changes one of a window's properties.</summary>
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newValue);

    /// <summary>Windows API: moves, resizes or redraws a window, depending on the flags.</summary>
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter,
        int x, int y, int width, int height, uint flags);
}
