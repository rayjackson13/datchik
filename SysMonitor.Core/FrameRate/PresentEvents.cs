using Microsoft.Diagnostics.Tracing;

namespace SysMonitor.Core.FrameRate;

/// <summary>
/// Identifies the Windows events that mean "a program is presenting a frame".
/// </summary>
internal static class PresentEvents
{
    /// <summary>ID of the "Microsoft-Windows-DXGI" event provider, used by DirectX 10, 11 and 12.</summary>
    public static readonly Guid DxgiProviderId = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

    /// <summary>ID of the "Microsoft-Windows-D3D9" event provider, used by DirectX 9 (and by WPF itself).</summary>
    public static readonly Guid D3d9ProviderId = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    /// <summary>Event number that DXGI sends when a frame starts being presented.</summary>
    public const int DxgiPresentStartEventId = 42;

    /// <summary>Event number that D3D9 sends when a frame starts being presented.</summary>
    public const int D3d9PresentStartEventId = 1;

    /// <summary>Returns true if the event means "a frame is being presented", from either provider.</summary>
    /// <param name="traceEvent">An event received from the ETW session.</param>
    public static bool IsPresentEvent(TraceEvent traceEvent) =>
        (traceEvent.ProviderGuid == DxgiProviderId && (int)traceEvent.ID == DxgiPresentStartEventId) ||
        (traceEvent.ProviderGuid == D3d9ProviderId && (int)traceEvent.ID == D3d9PresentStartEventId);
}
