// ============================================================================
// SensorSpike: a throwaway experiment that reads the CPU and GPU sensors we
// need and prints them once a second.
//
// HOW IT WORKS
// LibreHardwareMonitor (LHM) knows how to read sensors on many CPUs, GPUs and
// motherboards. It organizes everything as a tree:
//
//   Computer
//     └ Hardware        e.g. "AMD Ryzen 7 5700X", "NVIDIA GeForce RTX 4070 SUPER", the motherboard
//         ├ Sensors     e.g. "CPU Total" (load), "Package" (power)
//         └ SubHardware e.g. the motherboard's Nuvoton chip, which holds the fan sensors
//
// Sensors don't refresh on their own: each second we call Update() on every
// piece of hardware, then read each sensor's Value.
//
// MUST RUN AS ADMINISTRATOR. CPU temperature, power, clocks and motherboard
// fans are read through the PawnIO driver, which requires admin rights.
// Without them those values show as 0 or are missing.
//
// HOW TO READ THE COMMENTS
//   ///  documentation comments on class members: shown in the hover popup
//   //   regular comments: also shown in the popup when directly above a local variable
// ============================================================================

// Import LHM's classes so we can use them by short name:
// Computer, IHardware, ISensor, HardwareType, SensorType.
using LibreHardwareMonitor.Hardware;

// The entry point into LHM: represents this PC and all its monitored hardware.
// Each "Is...Enabled = true" switches on one group of hardware. We enable only what we
// need: fewer devices means less work per refresh, and less risk of stutters (polling
// some devices, like drives, is known to cause small hitches in games).
//   { ... } right after "new Computer" sets properties while creating the object.
var computer = new Computer
{
    IsCpuEnabled = true,         // CPU load, temperature, clock, power
    IsGpuEnabled = true,         // GPU load, temperature, clock, voltage, power, fans
    IsMotherboardEnabled = true, // motherboard fans (the CPU fan is plugged into the motherboard)
};

// Detect the hardware and connect to the driver. This takes a moment the first time.
computer.Open();

// When you press Ctrl+C, release the hardware and the driver cleanly before exiting.
//   "+="            = add a function to the list called when Ctrl+C is pressed.
//   "(_, _) => ..." = a lambda (small unnamed function); the "_" are parameters we don't need.
Console.CancelKeyPress += (_, _) => computer.Close();

// ---------------------------------------------------------------------------
// MAIN LOOP: refresh the sensors and print them once a second
// ---------------------------------------------------------------------------

while (true) // loops until you press Ctrl+C
{
    // Refresh every sensor value in the hardware tree.
    // Note: load % is calculated from the difference between two refreshes,
    // so it may show 0 on the very first pass.
    HardwareTree.UpdateAll(computer);

    Console.Clear();

    Console.WriteLine("CPU");
    ConsoleOutput.ShowReading("Util", SensorLookup.Find(computer, KnownSensors.CpuUtilization), "%");
    ConsoleOutput.ShowReading("Temp", SensorLookup.Find(computer, KnownSensors.CpuTemperature), "°C");
    ConsoleOutput.ShowReading("Clock", SensorLookup.Find(computer, KnownSensors.CpuClock), "MHz");
    ConsoleOutput.ShowReading("Power", SensorLookup.Find(computer, KnownSensors.CpuPower), "W");
    ConsoleOutput.ShowReading("Fan", SensorLookup.Find(computer, KnownSensors.CpuFan), "RPM");

    Console.WriteLine("GPU");
    ConsoleOutput.ShowReading("Util", SensorLookup.Find(computer, KnownSensors.GpuUtilization), "%");
    ConsoleOutput.ShowReading("Temp", SensorLookup.Find(computer, KnownSensors.GpuTemperature), "°C");
    ConsoleOutput.ShowReading("Clock", SensorLookup.Find(computer, KnownSensors.GpuClock), "MHz");
    ConsoleOutput.ShowReading("Voltage", SensorLookup.Find(computer, KnownSensors.GpuVoltage), "V");
    ConsoleOutput.ShowReading("Power", SensorLookup.Find(computer, KnownSensors.GpuPower), "W");
    ConsoleOutput.ShowReading("Fan", SensorLookup.Find(computer, KnownSensors.GpuFan), "RPM");

    Thread.Sleep(SensorSpikeSettings.RefreshIntervalMs); // wait before the next refresh
}

// ============================================================================
// TYPES
// In a file with top-level code (like the code above), type declarations must
// come after all of it. "static class" = a class you never create objects of;
// it just groups related values and functions under one name.
// ============================================================================

/// <summary>
/// Tuning values for this spike.
/// </summary>
static class SensorSpikeSettings
{
    /// <summary>How often sensors are refreshed and printed, in milliseconds (1000 = once a second).</summary>
    public const int RefreshIntervalMs = 1000;
}

/// <summary>
/// Identifies one sensor in LHM's hardware tree: which kind of hardware it's on,
/// what it measures, and its exact name as LHM reports it.
/// </summary>
/// <param name="HardwareType">The kind of hardware the sensor belongs to, e.g. Cpu, GpuNvidia, SuperIO.</param>
/// <param name="SensorType">What the sensor measures, e.g. Load, Temperature, Fan.</param>
/// <param name="Name">The sensor's name exactly as LHM reports it, e.g. "CPU Total".</param>
// "record" = a class meant to just hold data. This one line creates a type with three
// read-only properties and a constructor that takes all three.
record SensorId(HardwareType HardwareType, SensorType SensorType, string Name);

/// <summary>
/// The exact sensors used for each metric on this PC (Ryzen 7 5700X, B450M Pro4-F, RTX 4070 SUPER).
/// Sensor names differ between hardware: an Intel CPU, for example, names its temperature
/// and power sensors differently. These names came from the full sensor dump.
/// </summary>
static class KnownSensors
{
    // "static readonly" = set once when the program starts, never changed afterwards.
    // "new(...)" without a type name: the type (SensorId) is taken from the declaration on the left.

    /// <summary>Total CPU load across all cores, in percent.</summary>
    public static readonly SensorId CpuUtilization = new(HardwareType.Cpu, SensorType.Load, "CPU Total");

    /// <summary>CPU temperature as a single value (not per core), in °C. "Tctl/Tdie" is AMD's naming.</summary>
    public static readonly SensorId CpuTemperature = new(HardwareType.Cpu, SensorType.Temperature, "Core (Tctl/Tdie)");

    /// <summary>
    /// Average clock of the CPU cores while they're awake, in MHz. Matches what Core Temp shows.
    /// (The alternative, "Cores (Average Effective)", also counts sleep time and is much jumpier.)
    /// </summary>
    public static readonly SensorId CpuClock = new(HardwareType.Cpu, SensorType.Clock, "Cores (Average)");

    /// <summary>Power drawn by the whole CPU package, in watts.</summary>
    public static readonly SensorId CpuPower = new(HardwareType.Cpu, SensorType.Power, "Package");

    /// <summary>
    /// CPU cooler fan speed, in RPM. Read from the motherboard's monitoring chip (SuperIO).
    /// "Fan #1" is a guess; the correct number depends on which header the cooler is plugged into.
    /// </summary>
    public static readonly SensorId CpuFan = new(HardwareType.SuperIO, SensorType.Fan, "Fan #1");

    /// <summary>GPU core load, in percent.</summary>
    public static readonly SensorId GpuUtilization = new(HardwareType.GpuNvidia, SensorType.Load, "GPU Core");

    /// <summary>GPU core temperature, in °C. (Hot Spot and Memory Junction are separate sensors.)</summary>
    public static readonly SensorId GpuTemperature = new(HardwareType.GpuNvidia, SensorType.Temperature, "GPU Core");

    /// <summary>GPU core clock, in MHz.</summary>
    public static readonly SensorId GpuClock = new(HardwareType.GpuNvidia, SensorType.Clock, "GPU Core");

    /// <summary>GPU core voltage, in volts.</summary>
    public static readonly SensorId GpuVoltage = new(HardwareType.GpuNvidia, SensorType.Voltage, "GPU Core Voltage");

    /// <summary>
    /// Power drawn by the GPU, in watts. Not to be confused with "GPU Power",
    /// which is a Load sensor showing percent of the card's power limit.
    /// </summary>
    public static readonly SensorId GpuPower = new(HardwareType.GpuNvidia, SensorType.Power, "GPU Package");

    /// <summary>GPU fan speed, in RPM. This card reports two fans at nearly the same speed; we show the first.</summary>
    public static readonly SensorId GpuFan = new(HardwareType.GpuNvidia, SensorType.Fan, "GPU Fan 1");
}

/// <summary>
/// Refreshes sensor values across LHM's hardware tree.
/// </summary>
static class HardwareTree
{
    /// <summary>
    /// Refreshes every piece of hardware in the computer, including sub-hardware
    /// like the motherboard's monitoring chip.
    /// </summary>
    /// <param name="computer">The opened LHM computer.</param>
    public static void UpdateAll(Computer computer)
    {
        foreach (IHardware hardware in computer.Hardware)
        {
            UpdateWithChildren(hardware);
        }
    }

    /// <summary>
    /// Refreshes one piece of hardware, then all of its sub-hardware.
    /// </summary>
    /// <param name="hardware">The hardware to refresh.</param>
    // This function calls itself for each child ("recursion"), so it works
    // however many levels deep the tree goes.
    private static void UpdateWithChildren(IHardware hardware)
    {
        hardware.Update();

        foreach (IHardware subHardware in hardware.SubHardware)
        {
            UpdateWithChildren(subHardware);
        }
    }
}

/// <summary>
/// Finds specific sensors in LHM's hardware tree.
/// </summary>
static class SensorLookup
{
    /// <summary>
    /// Finds the sensor matching the given ID, or returns null if this PC doesn't have it.
    /// </summary>
    /// <param name="computer">The opened LHM computer.</param>
    /// <param name="sensorId">Which sensor to look for.</param>
    // "ISensor?" = returns a sensor OR null. The "?" warns callers that null is possible.
    //
    // The body is a LINQ chain: each step takes a list and produces a new one.
    // Read it top to bottom:
    //   1. SelectMany(... Prepend ...): make one flat list of all hardware plus its
    //      sub-hardware, so the motherboard chip (where the fans live) is included.
    //   2. Where(...): keep only hardware of the kind we want (e.g. GpuNvidia).
    //   3. SelectMany(... Sensors): collect all the sensors of that hardware into one list.
    //   4. FirstOrDefault(...): return the first sensor with the right type and name,
    //      or null if none matches.
    // Searching every second is fine for a spike; the real app will find each sensor once and keep it.
    public static ISensor? Find(Computer computer, SensorId sensorId)
    {
        return computer.Hardware
            .SelectMany(hardware => hardware.SubHardware.Prepend(hardware))
            .Where(hardware => hardware.HardwareType == sensorId.HardwareType)
            .SelectMany(hardware => hardware.Sensors)
            .FirstOrDefault(sensor => sensor.SensorType == sensorId.SensorType && sensor.Name == sensorId.Name);
    }
}

/// <summary>
/// Prints sensor readings to the console.
/// </summary>
static class ConsoleOutput
{
    /// <summary>
    /// Prints one line like "  Temp       51,6 °C", or a dash if the sensor or its value is missing.
    /// </summary>
    /// <param name="label">The text shown on the left, e.g. "Temp".</param>
    /// <param name="sensor">The sensor to read, or null if it wasn't found.</param>
    /// <param name="unit">The unit shown after the value, e.g. "°C".</param>
    public static void ShowReading(string label, ISensor? sensor, string unit)
    {
        // Several pieces of C# syntax in one line:
        //   sensor?.Value     = "if sensor is null, the result is null; otherwise read its Value".
        //                       Value is "float?" (a decimal number or null), since a sensor can
        //                       temporarily have no reading.
        //   is float value    = "if the result is a real number, put it in a variable named value".
        //   condition ? a : b = "if condition is true, use a, otherwise b" (the ternary operator).
        //   {value:0.#}       = format with at most one decimal place.
        string valueText = sensor?.Value is float value ? $"{value:0.#} {unit}" : "—";

        // {label,-10} = pad the label to 10 characters, left-aligned, so the values line up.
        Console.WriteLine($"  {label,-10} {valueText}");
    }
}  