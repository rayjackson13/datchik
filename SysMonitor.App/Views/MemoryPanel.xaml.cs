using System.Windows.Controls; // UserControl

namespace SysMonitor.App.Views;

/// <summary>
/// The Memory section. Everything is bound to its MemoryViewModel.
/// </summary>
public partial class MemoryPanel : UserControl
{
    /// <summary>Builds the panel from MemoryPanel.xaml.</summary>
    public MemoryPanel()
    {
        InitializeComponent();
    }
}
