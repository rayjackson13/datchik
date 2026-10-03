using CommunityToolkit.Mvvm.ComponentModel; // ObservableObject, ObservableProperty
using CommunityToolkit.Mvvm.Input;          // RelayCommand
using SysMonitor.App.Formatting;            // ValueFormatter
using SysMonitor.App.Settings;              // AppSettings, SettingsStore
using SysMonitor.Core.Hardware;             // MetricsSnapshot, FanSpeed
using System.Collections.ObjectModel;       // ObservableCollection
using System.Windows.Media.Animation;

namespace SysMonitor.App.ViewModels;

/// <summary>
/// What the CPU section shows, plus the CPU fan selector: which motherboard fan
/// is the CPU fan, and the menu for choosing it.
/// </summary>
internal sealed partial class CpuViewModel : ObservableObject
{
    /// <summary>The app settings; holds and saves the chosen CPU fan.</summary>
    private readonly AppSettings _settings;

    /// <summary>The motherboard fans from the latest reading, kept so a new choice can be shown right away.</summary>
    private IReadOnlyList<FanSpeed> _latestFans = [];

    [ObservableProperty] private string _utilization = ValueFormatter.MissingValue;
    [ObservableProperty] private string _temperature = ValueFormatter.MissingValue;
    [ObservableProperty] private string _clock = ValueFormatter.MissingValue;
    [ObservableProperty] private string _power = ValueFormatter.MissingValue;

    /// <summary>The chosen CPU fan's speed, e.g. "951 RPM".</summary>
    [ObservableProperty] private string _fan = ValueFormatter.MissingValue;

    /// <summary>The fan selector's text, e.g. "Fan #2 ▾".</summary>
    [ObservableProperty] private string _selectedFanLabel = string.Empty;

    /// <summary>
    /// The entries of the CPU fan menu. An ObservableCollection tells bindings when items are
    /// added or removed, so the menu updates by itself.
    /// </summary>
    public ObservableCollection<FanOptionViewModel> FanOptions { get; } = [];

    /// <summary>Creates the CPU section, using the settings for the chosen fan.</summary>
    /// <param name="settings">The app settings.</param>
    public CpuViewModel(AppSettings settings)
    {
        _settings = settings;
        SelectedFanLabel = $"{_settings.CpuFanSensorName} ▾";
    }

    /// <summary>
    /// Shows new sensor values, and refreshes the fan menu entries.
    /// </summary>
    /// <param name="metrics">The latest sensor values.</param>
    public void Update(MetricsSnapshot metrics)
    {
        Utilization = ValueFormatter.FormatValue(metrics.CpuUtilizationPercent, "%");
        Temperature = ValueFormatter.FormatValue(metrics.CpuTemperatureCelsius, "°C");
        Clock = ValueFormatter.FormatValue(metrics.CpuClockMhz, "MHz");
        Power = ValueFormatter.FormatValue(metrics.CpuPowerWatts, "W");

        _latestFans = metrics.MotherboardFans;
        UpdateFanSpeed();
        UpdateFanOptions();
    }

    /// <summary>
    /// Makes the given motherboard fan the CPU fan: saves the choice and updates the display.
    /// </summary>
    /// <param name="fanName">The fan's name, e.g. "Fan #2".</param>
    // [RelayCommand] generates a "SelectFanCommand" property that the menu items bind to.
    // The fan name arrives as the command's parameter.
    [RelayCommand]
    private void SelectFan(string fanName)
    {
        _settings.CpuFanSensorName = fanName;
        SettingsStore.Save(_settings);

        SelectedFanLabel = $"{fanName} ▾";
        UpdateFanSpeed();

        foreach (FanOptionViewModel fanOption in FanOptions)
        {
            fanOption.IsSelected = fanOption.Name == fanName;
        }
    }

    /// <summary>Shows the speed of the fan chosen as the CPU fan.</summary>
    private void UpdateFanSpeed()
    {
        FanSpeed? cpuFan = _latestFans.FirstOrDefault(fan => fan.Name == _settings.CpuFanSensorName);
        Fan = ValueFormatter.FormatValue(cpuFan?.Rpm, "RPM");
    }

    /// <summary>
    /// Keeps the menu entries in sync with the motherboard fans. The entries are only rebuilt
    /// if the set of fans changed; otherwise just their text and checkmarks are updated,
    /// so an open menu doesn't flicker or close.
    /// </summary>
    private void UpdateFanOptions()
    {
        // SequenceEqual = "do both lists contain the same items, in the same order?"
        bool sameFansAsBefore = FanOptions
            .Select(fanOption => fanOption.Name)
            .SequenceEqual(_latestFans.Select(fan => fan.Name));

        if (!sameFansAsBefore)
        {
            FanOptions.Clear();
            foreach (FanSpeed fan in _latestFans)
            {
                FanOptions.Add(new FanOptionViewModel(fan.Name));
            }
        }

        // Both lists now hold the same fans in the same order, so position i matches in both.
        for (int fanIndex = 0; fanIndex < _latestFans.Count; fanIndex++)
        {
            FanSpeed fan = _latestFans[fanIndex];
            FanOptionViewModel fanOption = FanOptions[fanIndex];

            fanOption.Label = $"{fan.Name}  —  {ValueFormatter.FormatValue(fan.Rpm, "RPM")}";
            fanOption.IsSelected = fan.Name == _settings.CpuFanSensorName;
        }
    }
}
