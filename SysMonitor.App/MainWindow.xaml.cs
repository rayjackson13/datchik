using System.Diagnostics;            // Debug
using System.Windows;                // Window, RoutedEventArgs, Visibility, UIElement, FrameworkElement
using System.Windows.Media;          // Color, Colors, SolidColorBrush
using System.Windows.Threading;      // DispatcherTimer, DispatcherPriority
using SysMonitor.App.WindowStyling;  // TitleBar, TitleBarIcon (use your folder's namespace)
using SysMonitor.Core.FrameRate;     // FrameRateTracker, FrameRateReading
using SysMonitor.Core.Hardware;      // HardwareMonitor, MetricsSnapshot

namespace SysMonitor.App;

/// <summary>
/// The main window. Shows FPS (refreshed often, on the UI thread) and sensor values
/// (read on a background thread, so slow hardware access never freezes the window).
/// </summary>
public partial class MainWindow : Window
{
    // ---- Settings ----

    /// <summary>How often FPS and 1% low are refreshed on screen.</summary>
    private static readonly TimeSpan FrameRateRefreshInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>How often the hardware sensors are read and refreshed on screen.</summary>
    private static readonly TimeSpan SensorRefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long to wait for the sensor loop to finish when the window closes,
    /// so it can release the hardware cleanly.
    /// </summary>
    private static readonly TimeSpan SensorShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The Tag value that marks rows shown only in extended mode (set in MainWindow.xaml).</summary>
    private const string ExtendedOnlyTag = "Extended";

    // ---- State ----

    /// <summary>Measures the FPS and 1% low of the game being played.</summary>
    private readonly FrameRateTracker _frameRateTracker = new();

    /// <summary>Calls <see cref="ShowFrameRate"/> on the UI thread every <see cref="FrameRateRefreshInterval"/>.</summary>
    private readonly DispatcherTimer _frameRateTimer = new() { Interval = FrameRateRefreshInterval };

    /// <summary>
    /// The "please stop" signal for the background sensor loop, triggered when the window closes.
    /// A CancellationTokenSource is the sender; the CancellationToken it hands out is the receiver.
    /// </summary>
    private readonly CancellationTokenSource _shutdownSignal = new();

    /// <summary>
    /// The background sensor loop. Kept so that, on close, we can wait for it to finish
    /// and release the hardware.
    /// </summary>
    private readonly Task _sensorLoop;

    /// <summary>True = show every metric; false = basic mode, with only the essentials.</summary>
    private bool _isExtendedMode = false;

    /// <summary>
    /// Sets up the window, starts the FPS timer and the background sensor loop.
    /// Returns quickly, so the window appears right away.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        // Start in basic mode: hide the extended-only rows and set the button's text.
        ApplyDisplayMode();

        // Once the window exists on the Windows side, style its title bar.
        SourceInitialized += (_, _) =>
        {
            Color titleBarColor = Background is SolidColorBrush backgroundBrush
                ? backgroundBrush.Color
                : Colors.Black;
            DarkTitleBar.Apply(this, titleBarColor);
            TitleBarIcon.Remove(this);
        };

        // FPS: refreshed on the UI thread by a timer. Reading it is cheap, so that's fine.
        _frameRateTimer.Tick += (_, _) => ShowFrameRate();
        _frameRateTimer.Start();

        // Sensors: read on a background thread.
        // Task.Run starts the loop on a background thread and returns immediately,
        // so the constructor doesn't wait for hardware detection (1–2 seconds).
        _sensorLoop = Task.Run(() => RunSensorLoopAsync(_shutdownSignal.Token));

        Closed += (_, _) => ShutDown();
    }

    /// <summary>
    /// The background sensor loop: detects the hardware once, then reads the sensors every
    /// <see cref="SensorRefreshInterval"/> and hands the values to the UI thread to display.
    /// Runs until <paramref name="cancellationToken"/> is triggered.
    /// </summary>
    /// <param name="cancellationToken">The "please stop" signal.</param>
    // "async Task" = this method can pause at each "await" without blocking its thread,
    // and the caller gets a Task representing the whole run.
    private async Task RunSensorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Created HERE, on the background thread: hardware detection is the slow part,
            // and doing it here keeps the window responsive. "using var" releases the
            // hardware when the loop ends.
            using var hardwareMonitor = new HardwareMonitor();
            using var sensorTimer = new PeriodicTimer(SensorRefreshInterval);

            // do { ... } while (...) runs the body FIRST, then checks the condition,
            // so the first values appear immediately instead of after one interval.
            do
            {
                MetricsSnapshot metrics = hardwareMonitor.ReadMetrics();

                // Only the UI thread may change what's on screen, so hand the update to it.
                // Passing the cancellation token means: if we're shutting down, drop this update.
                await Dispatcher.InvokeAsync(
                    () => ShowSensorMetrics(metrics),
                    DispatcherPriority.Normal,
                    cancellationToken);
            }
            // Waits for the next tick. Returns false (ending the loop) if the timer is disposed,
            // and throws OperationCanceledException if the "please stop" signal is triggered.
            while (await sensorTimer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Expected: this is how the loop ends when the window closes. Nothing to do.
        }
        catch (Exception exception)
        {
            // Anything else (e.g. hardware detection failed): the sensor rows stay as "—".
            // Debug.WriteLine prints to Visual Studio's Output window when running with F5.
            Debug.WriteLine($"Sensor loop stopped: {exception}");
        }
    }

    /// <summary>
    /// Reads the game's frame rate and shows it. Runs on the UI thread, every <see cref="FrameRateRefreshInterval"/>.
    /// </summary>
    private void ShowFrameRate()
    {
        // "FrameRateReading?" = null when no game is running.
        FrameRateReading? frameRate = _frameRateTracker.Read();

        GameNameText.Text = frameRate?.GameName ?? "—";
        CurrentFpsText.Text = frameRate is null ? "—" : frameRate.CurrentFps.ToString();
        OnePercentLowText.Text = frameRate is null ? "—" : frameRate.OnePercentLowFps.ToString("0");
    }

    /// <summary>
    /// Shows sensor values in the window. Runs on the UI thread, called by the sensor loop.
    /// </summary>
    /// <param name="metrics">The values just read by the background loop.</param>
    private void ShowSensorMetrics(MetricsSnapshot metrics)
    {
        CpuUtilizationText.Text = FormatValue(metrics.CpuUtilizationPercent, "%");
        CpuTemperatureText.Text = FormatValue(metrics.CpuTemperatureCelsius, "°C");
        CpuClockText.Text = FormatValue(metrics.CpuClockMhz, "MHz");
        CpuPowerText.Text = FormatValue(metrics.CpuPowerWatts, "W");
        CpuFanText.Text = FormatValue(metrics.CpuFanRpm, "RPM");

        RamUsageText.Text = FormatUsage(metrics.RamUsedGigabytes, metrics.RamTotalGigabytes);

        GpuUtilizationText.Text = FormatValue(metrics.GpuUtilizationPercent, "%");
        GpuTemperatureText.Text = FormatValue(metrics.GpuTemperatureCelsius, "°C");
        GpuClockText.Text = FormatValue(metrics.GpuClockMhz, "MHz");
        GpuVoltageText.Text = FormatValue(metrics.GpuVoltageVolts, "V", decimalFormat: "0.00");
        GpuPowerText.Text = FormatValue(metrics.GpuPowerWatts, "W");
        GpuFanText.Text = FormatValue(metrics.GpuFanRpm, "RPM");

        // VRAM arrives in megabytes; divide by 1024 to show gigabytes, like RAM.
        GpuMemoryText.Text = FormatUsage(
            metrics.GpuMemoryUsedMegabytes / 1024.0,
            metrics.GpuMemoryTotalMegabytes / 1024.0);
    }

    /// <summary>
    /// Stops everything when the window closes: the FPS timer, the FPS tracker,
    /// and the background sensor loop (waiting briefly so it can release the hardware).
    /// </summary>
    private void ShutDown()
    {
        _frameRateTimer.Stop();

        // Send the "please stop" signal, then give the loop a moment to finish.
        // If it's in the middle of hardware detection and takes longer, we stop waiting;
        // Windows releases everything anyway when the app exits.
        _shutdownSignal.Cancel();
        _sensorLoop.Wait(SensorShutdownTimeout);

        _frameRateTracker.Dispose();
        _shutdownSignal.Dispose();
    }

    /// <summary>
    /// Runs when the "Show more / Show less" button is clicked: switches between basic and extended mode.
    /// </summary>
    /// <param name="sender">The button that was clicked (not needed here).</param>
    /// <param name="e">Details about the click (not needed here).</param>
    private void DisplayModeButton_Click(object sender, RoutedEventArgs e)
    {
        _isExtendedMode = !_isExtendedMode;
        ApplyDisplayMode();
    }

    /// <summary>
    /// Shows or hides the extended-only rows to match <see cref="_isExtendedMode"/>,
    /// and updates the button's text. The window resizes itself to fit.
    /// </summary>
    private void ApplyDisplayMode()
    {
        // Collapsed = hidden AND takes up no space, so the window shrinks.
        Visibility extendedRowVisibility = _isExtendedMode ? Visibility.Visible : Visibility.Collapsed;

        foreach (UIElement element in MetricsPanel.Children)
        {
            if (element is FrameworkElement { Tag: ExtendedOnlyTag } extendedOnlyRow)
            {
                extendedOnlyRow.Visibility = extendedRowVisibility;
            }
        }

        DisplayModeButton.Content = _isExtendedMode ? "Show less ▴" : "Show more ▾";
    }

    /// <summary>
    /// Turns a sensor value into display text like "52 °C", or "—" if there's no value.
    /// </summary>
    /// <param name="value">The sensor value, or null if unavailable.</param>
    /// <param name="unit">The unit to show after the number.</param>
    /// <param name="decimalFormat">How to format the number: "0" = no decimals (default), "0.00" = two.</param>
    private static string FormatValue(float? value, string unit, string decimalFormat = "0")
    {
        if (value is float actualValue)
        {
            return $"{actualValue.ToString(decimalFormat)} {unit}";
        }

        return "—";
    }

    /// <summary>
    /// Turns a used/total pair into text like "12.3 / 32.0 GB", or "—" if either value is missing.
    /// </summary>
    /// <param name="usedGigabytes">Amount in use, in GB.</param>
    /// <param name="totalGigabytes">Total amount, in GB.</param>
    private static string FormatUsage(double? usedGigabytes, double? totalGigabytes)
    {
        if (usedGigabytes is double used && totalGigabytes is double total)
        {
            return $"{used:0.0} / {total:0.0} GB";
        }

        return "—";
    }
}
