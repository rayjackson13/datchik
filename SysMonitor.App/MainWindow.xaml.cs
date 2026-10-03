using System.Windows;                     // Window, RoutedEventArgs
using System.Windows.Controls;            // Button, ContextMenu
using System.Windows.Controls.Primitives; // PlacementMode
using System.Windows.Media;               // Color, Colors, SolidColorBrush
using SysMonitor.App.Settings;            // AppSettings, SettingsStore
using SysMonitor.App.ViewModels;          // MainViewModel
using SysMonitor.App.WindowStyling;       // DarkTitleBar, TitleBarIcon, WindowPlacement

namespace SysMonitor.App;

/// <summary>
/// The main window. Everything it shows comes from <see cref="MainViewModel"/> through bindings;
/// this code-behind only handles things about the window itself: title bar styling, window
/// position, and opening the CPU fan menu on left-click.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>Everything Datchik remembers between runs, loaded at startup.</summary>
    private readonly AppSettings _settings = SettingsStore.Load();

    /// <summary>The ViewModel: all the data and logic behind this window.</summary>
    private readonly MainViewModel _viewModel;

    /// <summary>
    /// Creates the window, connects it to its ViewModel, and starts the data loops.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Connect the window to its ViewModel: every {Binding ...} in MainWindow.xaml reads from it.
        _viewModel = new MainViewModel(_settings);
        DataContext = _viewModel;

        WindowPlacement.Restore(this, _settings);

        // Once the window exists on the Windows side, style its title bar.
        SourceInitialized += (_, _) => ApplyTitleBarStyle();

        Closed += (_, _) =>
        {
            WindowPlacement.Save(this, _settings);
            _viewModel.ShutDown();
        };

        _viewModel.Start();
    }

    /// <summary>
    /// Makes the title bar dark, the same color as the window background, and removes its icon.
    /// </summary>
    private void ApplyTitleBarStyle()
    {
        Color titleBarColor = Background is SolidColorBrush backgroundBrush
            ? backgroundBrush.Color
            : Colors.Black;

        DarkTitleBar.Apply(this, titleBarColor);
        TitleBarIcon.Remove(this);
    }

    /// <summary>
    /// Runs when the CPU fan selector is clicked: opens its menu right below it.
    /// (A context menu normally opens only on right-click, so we open it ourselves.
    /// That's purely about the view, so it belongs here rather than in a ViewModel.)
    /// </summary>
    /// <param name="sender">The button that was clicked.</param>
    /// <param name="e">Details about the click (not needed here).</param>
    private void CpuFanSelectorButton_Click(object sender, RoutedEventArgs e)
    {
        // A nested property pattern: "is the sender a Button whose ContextMenu is a ContextMenu?"
        // If so, both are available by name: selectorButton and fanMenu.
        if (sender is not Button { ContextMenu: ContextMenu fanMenu } selectorButton)
        {
            return;
        }

        fanMenu.PlacementTarget = selectorButton;
        fanMenu.Placement = PlacementMode.Bottom;
        fanMenu.IsOpen = true;
    }
}
