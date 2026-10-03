using System.Diagnostics;                   // Debug
using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty, NotifyPropertyChangedFor
using CommunityToolkit.Mvvm.Input;          // RelayCommand
using SysMonitor.App.Settings;              // AppSettings, SettingsStore
using SysMonitor.Core.FrameRate;            // FrameRateTracker
using SysMonitor.Core.Hardware;             // HardwareMonitor, MetricsSnapshot

namespace SysMonitor.App.ViewModels;

/// <summary>
/// The window's ViewModel: owns the section ViewModels, runs the FPS and sensor loops,
/// and manages the display mode. Knows nothing about windows, buttons or text blocks.
/// </summary>
/// <remarks>
/// Create it on the UI thread, then call <see cref="Start"/>. Both loops run on the UI thread;
/// only the slow hardware work is sent to a background thread (await Task.Run). After each
/// await, the code continues on the UI thread, so the section ViewModels are always updated
/// there, which is what WPF bindings need.
/// </remarks>
internal sealed partial class MainViewModel : ObservableObject
{
    /// <summary>How often FPS and 1% low are refreshed.</summary>
    private static readonly TimeSpan FrameRateRefreshInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>How often the hardware sensors are read.</summary>
    private static readonly TimeSpan SensorRefreshInterval = TimeSpan.FromSeconds(1);

    /// <summary>How long shutdown waits for an in-progress hardware read to finish.</summary>
    private static readonly TimeSpan SensorShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>The app settings; holds and saves the display mode.</summary>
    private readonly AppSettings _settings;

    /// <summary>Measures the FPS and 1% low of the game being played.</summary>
    private readonly FrameRateTracker _frameRateTracker = new();

    /// <summary>The "please stop" signal for both loops, triggered on shutdown.</summary>
    private readonly CancellationTokenSource _shutdownSignal = new();

    /// <summary>Reads the sensors. null until hardware detection has finished in the background.</summary>
    private HardwareMonitor? _hardwareMonitor;

    /// <summary>
    /// The hardware work currently running in the background (detection or a read), if any.
    /// Shutdown waits for it before releasing the hardware.
    /// </summary>
    private Task? _currentSensorWork;

    /// <summary>The Frame Rate section.</summary>
    public FrameRateViewModel FrameRate { get; } = new();

    /// <summary>The CPU section, including the CPU fan selector.</summary>
    public CpuViewModel Cpu { get; }

    /// <summary>The GPU section.</summary>
    public GpuViewModel Gpu { get; } = new();

    /// <summary>The Memory section.</summary>
    public MemoryViewModel Memory { get; } = new();

    /// <summary>True = show every metric; false = basic mode.</summary>
    // [NotifyPropertyChangedFor] = whenever IsExtendedMode changes, also announce that
    // DisplayModeButtonText changed, since its text depends on IsExtendedMode.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayModeButtonText))]
    private bool _isExtendedMode;

    /// <summary>The text of the mode switch at the bottom of the window.</summary>
    // A calculated property: no field, just worked out from IsExtendedMode whenever it's read.
    public string DisplayModeButtonText => IsExtendedMode ? "Show less ▴" : "Show more ▾";

    /// <summary>Creates the ViewModel with the given settings.</summary>
    /// <param name="settings">The app settings, loaded at startup.</param>
    public MainViewModel(AppSettings settings)
    {
        _settings = settings;
        Cpu = new CpuViewModel(settings);
        IsExtendedMode = settings.IsExtendedMode;
    }

    /// <summary>
    /// Starts the FPS and sensor loops. Call once, on the UI thread.
    /// </summary>
    public void Start()
    {
        // "_ = ..." starts the loop without waiting for it. The loops run until shutdown;
        // the "_" (discard) says "I'm deliberately not keeping the returned Task".
        _ = RunFrameRateLoopAsync(_shutdownSignal.Token);
        _ = RunSensorLoopAsync(_shutdownSignal.Token);
    }

    /// <summary>
    /// Stops both loops and releases the hardware. Call when the window closes.
    /// </summary>
    public void ShutDown()
    {
        _shutdownSignal.Cancel();

        // Wait for any hardware work still running in the background before releasing the
        // hardware it uses. Safe to wait for here: that work runs on a background thread
        // and doesn't need the UI thread. Task.WaitAny returns after the timeout even if
        // the work isn't done, and doesn't throw if it failed.
        if (_currentSensorWork is not null)
        {
            Task.WaitAny([_currentSensorWork], SensorShutdownTimeout);
        }

        _hardwareMonitor?.Dispose();
        _frameRateTracker.Dispose();
        _shutdownSignal.Dispose();
    }

    /// <summary>Switches between basic and extended mode. Bound to the "Show more / Show less" button.</summary>
    [RelayCommand]
    private void ToggleDisplayMode()
    {
        IsExtendedMode = !IsExtendedMode;
    }

    /// <summary>
    /// Runs automatically whenever IsExtendedMode changes: remembers the new mode.
    /// </summary>
    /// <param name="value">The new value.</param>
    // The toolkit calls a method named On[PropertyName]Changed if you write one.
    // "partial void" = the toolkit declared it; we provide the body.
    partial void OnIsExtendedModeChanged(bool value)
    {
        _settings.IsExtendedMode = value;
        SettingsStore.Save(_settings);
    }

    /// <summary>
    /// Refreshes the frame rate every <see cref="FrameRateRefreshInterval"/> until shutdown.
    /// Reading it is cheap, so it all stays on the UI thread.
    /// </summary>
    /// <param name="cancellationToken">The "please stop" signal.</param>
    private async Task RunFrameRateLoopAsync(CancellationToken cancellationToken)
    {
        using var frameRateTimer = new PeriodicTimer(FrameRateRefreshInterval);

        try
        {
            while (await frameRateTimer.WaitForNextTickAsync(cancellationToken))
            {
                FrameRate.Update(_frameRateTracker.Read());
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    /// Detects the hardware, then reads the sensors every <see cref="SensorRefreshInterval"/>
    /// until shutdown. The slow parts run on a background thread; the display updates happen
    /// back on the UI thread.
    /// </summary>
    /// <param name="cancellationToken">The "please stop" signal.</param>
    private async Task RunSensorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Hardware detection takes a second or two, so do it on a background thread.
            Task<HardwareMonitor> detectionTask = Task.Run(() => new HardwareMonitor());
            _currentSensorWork = detectionTask;
            _hardwareMonitor = await detectionTask;

            using var sensorTimer = new PeriodicTimer(SensorRefreshInterval);

            // do { ... } while (...) runs the body first, so values appear right away.
            do
            {
                Task<MetricsSnapshot> readTask = Task.Run(_hardwareMonitor.ReadMetrics);
                _currentSensorWork = readTask;
                MetricsSnapshot metrics = await readTask;

                // Back on the UI thread here, so updating the ViewModels is safe.
                Cpu.Update(metrics);
                Gpu.Update(metrics);
                Memory.Update(metrics);
            }
            while (await sensorTimer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        catch (Exception exception)
        {
            // E.g. hardware detection failed: the sensor values stay as dashes.
            Debug.WriteLine($"Sensor loop stopped: {exception}");
        }
    }
}
