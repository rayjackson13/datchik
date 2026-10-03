using System.Windows.Controls; // UserControl

namespace SysMonitor.App.Views;

/// <summary>
/// The Frame Rate section: game, FPS and 1% low. Everything is bound to its FrameRateViewModel.
/// </summary>
public partial class FrameRatePanel : UserControl
{
    /// <summary>Builds the panel from FrameRatePanel.xaml.</summary>
    public FrameRatePanel()
    {
        InitializeComponent();
    }
}
