using System.Windows;                     // RoutedEventArgs
using System.Windows.Controls;            // UserControl, Button, ContextMenu
using System.Windows.Controls.Primitives; // PlacementMode

namespace SysMonitor.App.Views;

/// <summary>
/// The CPU section, including the CPU fan selector. Everything is bound to its CpuViewModel;
/// the only code here opens the fan menu on left-click, which is purely about the view.
/// </summary>
public partial class CpuPanel : UserControl
{
    /// <summary>Builds the panel from CpuPanel.xaml.</summary>
    public CpuPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Runs when the CPU fan selector is clicked: opens its menu right below it.
    /// (A context menu normally opens only on right-click, so we open it ourselves.)
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Details about the click (not needed here).</param>
    private void FanSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        // "Is the sender a Button whose ContextMenu is a ContextMenu?" If so, both get names.
        if (sender is not Button { ContextMenu: ContextMenu fanMenu } selectorButton)
        {
            return;
        }

        fanMenu.PlacementTarget = selectorButton;
        fanMenu.Placement = PlacementMode.Bottom;
        fanMenu.IsOpen = true;
    }
}
