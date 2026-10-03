using System.Windows.Controls; // UserControl

namespace SysMonitor.App.Views;

/// <summary>
/// The GPU section. Everything is bound to its GpuViewModel.
/// </summary>
public partial class GpuPanel : UserControl
{
    /// <summary>Builds the panel from GpuPanel.xaml.</summary>
    public GpuPanel()
    {
        InitializeComponent();
    }
}
