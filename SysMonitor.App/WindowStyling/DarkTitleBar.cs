using System.Runtime.InteropServices; // DllImport
using System.Windows;                 // Window
using System.Windows.Interop;         // WindowInteropHelper
using System.Windows.Media;           // Color

namespace SysMonitor.App.WindowStyling;

/// <summary>
/// Makes a window's title bar dark, using the Windows API that draws window frames (DWM).
/// WPF has no setting for this, because the title bar is drawn by Windows, not by WPF.
/// </summary>
// "internal" = usable anywhere in this project, but not from other projects.
internal static class DarkTitleBar
{
    /// <summary>DWM setting number: dark mode for the frame (white title text and buttons).</summary>
    private const int DwmUseImmersiveDarkMode = 20;

    /// <summary>DWM setting number: exact title bar color. Windows 11 only; ignored on Windows 10.</summary>
    private const int DwmCaptionColor = 35;

    /// <summary>
    /// Applies dark mode and the given color to the window's title bar.
    /// Call it from the window's SourceInitialized event: before that, the window
    /// doesn't exist on the Windows side yet, so there's nothing to apply it to.
    /// </summary>
    /// <param name="window">The window to change.</param>
    /// <param name="captionColor">The color for the title bar.</param>
    public static void Apply(Window window, Color captionColor)
    {
        // Windows identifies windows by a "handle" (a number). WPF keeps it hidden;
        // WindowInteropHelper gives us access to it.
        IntPtr windowHandle = new WindowInteropHelper(window).Handle;

        int enableDarkMode = 1; // 1 = on, 0 = off
        DwmSetWindowAttribute(windowHandle, DwmUseImmersiveDarkMode, ref enableDarkMode, sizeof(int));

        // Windows expects colors as one number laid out as 0x00BBGGRR (blue, green, red),
        // the reverse of the usual #RRGGBB order.
        //   "<< 8" shifts a value 8 bits to the left, moving it into the next byte.
        //   "|" combines the three bytes into one number.
        int windowsColor = captionColor.R | (captionColor.G << 8) | (captionColor.B << 16);
        DwmSetWindowAttribute(windowHandle, DwmCaptionColor, ref windowsColor, sizeof(int));
    }

    /// <summary>Windows API: changes one setting of a window's frame.</summary>
    /// <param name="windowHandle">The window to change.</param>
    /// <param name="attribute">Which setting, e.g. <see cref="DwmUseImmersiveDarkMode"/>.</param>
    /// <param name="value">The new value.</param>
    /// <param name="valueSize">Size of the value in bytes.</param>
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);
}
