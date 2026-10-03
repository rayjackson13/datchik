# Datchik

<img src="SysMonitor.App/Assets/datchik.ico" width="64" align="right" alt="Datchik icon">

A lightweight Windows app that shows your PC's performance while you game.

Play on one screen, keep Datchik open on the other: FPS, 1% low, CPU and GPU load, temperatures, clocks, power and memory, at a glance. No overlay, nothing injected into the game.

The name comes from the Russian «датчик», meaning *sensor*.

## Features

- **FPS and 1% low** for the game you're playing, detected automatically from the foreground window
- **CPU**: utilization, temperature, clock, power, fan speed
- **GPU**: utilization, temperature, clock, voltage, power, fan speed, VRAM usage
- **RAM** usage
- **Basic and extended modes**: basic shows only the essentials, extended shows everything
- **CPU fan picker**: click the fan row to choose which motherboard fan header is the CPU fan, with live speeds shown for each
- **Remembers your setup**: display mode, CPU fan choice and window position are kept between runs
- **Always on top** of other windows on its monitor, with a dark title bar
- **Lightweight by design**:
  - sensors are read once per second, on a background thread
  - only the hardware that's actually displayed is polled
  - no polling of drives or other devices known to cause in-game stutters

### What each mode shows

| Section | Basic | Extended adds |
|---|---|---|
| Frame Rate | FPS, 1% low, game | — |
| CPU | Utilization, temperature | Clock, power, fan |
| GPU | Utilization, temperature, fan, VRAM | Clock, voltage, power |
| Memory | RAM | — |

## Installing

1. Download `DatchikSetup-<version>.exe`.
2. Run it. Windows may show **"Windows protected your PC"**, because the installer isn't code-signed. Click **More info → Run anyway**.
3. Keep **"Download and install PawnIO"** ticked if the installer offers it. See [Requirements](#requirements) for what it's for.

The installer bundles the .NET runtime, so nothing else needs to be installed first. Datchik appears in the Start Menu, and can be uninstalled from **Settings → Apps** like any other program.

## Requirements

- **Windows 10 or 11**, 64-bit
- **[PawnIO](https://pawnio.eu/)** driver, needed for CPU temperature, clock, power and fan speeds.
  - The installer downloads and installs it automatically if it's missing.
  - Without it, Datchik still shows FPS, GPU data, CPU utilization and RAM.
- **Administrator rights.** Datchik asks for them each time it starts. Both the hardware driver and Windows' frame event tracing require them.

## Building from source

The only prerequisite is the **.NET 10 SDK**. Every other build tool, including the installer compiler, is downloaded automatically as a NuGet package, pinned to an exact version.

### Running during development

Open `SysMonitor.slnx` in Visual Studio, set **SysMonitor.App** as the startup project, and press **F5**. Accept when Visual Studio offers to restart as administrator.

Or, from an **administrator** terminal:

```
dotnet run --project SysMonitor.App
```

### Building the installer

One command, from the repository root:

```
dotnet publish SysMonitor.App -c Release
```

This cleans the previous output, publishes a self-contained build (with the .NET runtime bundled) into `publish/`, and compiles the installer with [Inno Setup](https://jrsoftware.org/isinfo.php). The result:

```
installer-output/DatchikSetup-<version>.exe
```

For quicker test builds, with lighter compression and a bigger file:

```
dotnet publish SysMonitor.App -c Release -p:FastInstaller=true
```

### Releasing a new version

The version lives in one place: `<Version>` in `SysMonitor.App/SysMonitor.App.csproj`. Change it and build again; the `.exe`, the installer and the installer's file name all follow from it. Because the installer's `AppId` never changes, running a newer installer upgrades an existing installation in place.

## How it works

### Hardware sensors

Sensors are read with [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
- **CPU and motherboard values** (including all fan headers) go through the PawnIO driver.
- **GPU values** come from NVIDIA's own driver.
- **RAM** is read directly from Windows (`GlobalMemoryStatusEx`), the same number Task Manager shows.

### FPS and 1% low

Every time a game finishes a frame, it calls a "Present" function to hand the frame to Windows. Windows reports these calls through **ETW** (Event Tracing for Windows). Datchik listens with [TraceEvent](https://github.com/microsoft/perfview/tree/main/src/TraceEvent) and records a timestamp for every frame.

- **FPS** is the number of frames presented in the last second.
- **1% low** is the 99th-percentile frame time over the last 30 seconds, converted to FPS. It answers: "how low does the frame rate drop during stutters?"

The game is chosen automatically: the foreground window, as long as it renders frames. Clicking on Datchik or another non-game window keeps the previous game selected, so it works with the game on another monitor.

### Settings

Settings are stored as JSON in `%AppData%\Datchik\settings.json`. If the file is missing or unreadable, Datchik starts with defaults. A saved window position is ignored if it's no longer on a connected screen, for example after unplugging a monitor.

## Project structure

The app follows the **MVVM** pattern: Views (XAML) bind to ViewModels (plain C# classes holding the displayed values and actions), which get their data from `SysMonitor.Core`.

```
SysMonitor.slnx
├─ Spikes/                      Early experiments, kept for reference
│  ├─ FpsSpike                  Console FPS measurement prototype
│  └─ SensorSpike               Console sensor reader
├─ SysMonitor.App/              The WPF app
│  ├─ Assets/                   App icon
│  ├─ Controls/                 MetricRow: the reusable label + value row
│  ├─ Formatting/               Turns sensor values into display text
│  ├─ Settings/                 AppSettings and its JSON storage
│  ├─ Themes/                   Styles.xaml: all colors, font sizes and styles
│  ├─ ViewModels/               Main, FrameRate, Cpu, Gpu and Memory ViewModels
│  ├─ Views/                    One panel per section
│  ├─ WindowStyling/            Dark title bar, icon removal, window position
│  ├─ App.xaml
│  └─ MainWindow.xaml(.cs)      Stacks the panels; connects the window to MainViewModel
├─ SysMonitor.Core/             Data collection, no UI
│  ├─ FrameRate/                ETW frame tracking, FPS and 1% low calculation
│  ├─ Hardware/                 Sensor reading, sensor mapping, RAM
│  └─ Processes/                Foreground process and process name lookups
└─ installer/
   └─ Datchik.iss               Inno Setup installer script
```

`SysMonitor.Core` has no UI dependencies, so it could be reused, for example by a future overlay.

## Known limitations

- **DirectX only.** FPS tracking covers DirectX 9–12 games. Vulkan and OpenGL games aren't detected yet.
- **NVIDIA only.** The GPU sensors are mapped for NVIDIA cards.
- **Hardware-specific sensor names.** The sensor mapping in `KnownSensors.cs` was made on one PC (Ryzen 7 5700X, ASRock B450M Pro4-F, RTX 4070 SUPER). Other CPUs and motherboards may name their sensors differently, in which case some values show "—".
- **FPS updates follow Windows' batches.** ETW delivers frame events in batches of about one second, so FPS can't be fresher than that.
- **Unsigned.** The installer and app aren't code-signed, so Windows SmartScreen warns on first run.

## Roadmap

- [x] Persisted settings: display mode, CPU fan selection, window position
- [x] App icon and installer, with the .NET runtime bundled
- [x] Automatic PawnIO installation
- [ ] Configurable sensor mapping for other hardware (Intel CPUs, other motherboards)
- [ ] Vulkan / OpenGL FPS support (e.g. via PresentMon)
- [ ] Optional in-game overlay

## Acknowledgements

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor): hardware sensor access
- [PawnIO](https://pawnio.eu/): signed driver for low-level hardware access
- [TraceEvent](https://github.com/microsoft/perfview/tree/main/src/TraceEvent): Microsoft's library for reading ETW events
- [PresentMon](https://github.com/GameTechDev/PresentMon): the reference for measuring frame rates through ETW
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet): MVVM helpers and source generators
- [Inno Setup](https://jrsoftware.org/isinfo.php): the installer, built via the [Tools.InnoSetup](https://www.nuget.org/packages/Tools.InnoSetup) NuGet package
