# Datchik

A lightweight Windows app that shows your PC's performance while you game.

Play on one screen, keep Datchik open on the other: FPS, 1% low, CPU and GPU load, temperatures, clocks, power and memory, at a glance. No overlay, nothing injected into the game.

The name comes from the Russian «датчик», meaning *sensor*.

## Features

- **FPS and 1% low** for the game you're playing, detected automatically from the foreground window
- **CPU**: utilization, temperature, clock, power, fan speed
- **GPU**: utilization, temperature, clock, voltage, power, fan speed, VRAM usage
- **RAM** usage
- **Basic and extended modes**: basic shows only the essentials, extended shows everything
- **Always on top** of other windows on its monitor
- **Lightweight by design**:
  - sensors are read once per second on a background thread
  - only the hardware that's actually displayed is polled
  - no polling of drives or other devices known to cause in-game stutters

### What each mode shows

| Section | Basic | Extended adds |
|---|---|---|
| FPS | Game, current FPS, 1% low | — |
| CPU | Utilization, temperature | Clock, power, fan |
| Memory | RAM | — |
| GPU | Utilization, temperature, fan, VRAM | Clock, voltage, power |

## Requirements

- **Windows 10 or 11**, 64-bit
- **.NET 10 Desktop Runtime**, or the .NET 10 SDK to build from source
- **[PawnIO](https://pawnio.eu/)** driver, needed for CPU temperature, clock, power and motherboard fan readings.
  - Install it once.
  - Without it, Datchik still shows FPS, GPU data, CPU utilization and RAM.
- **Administrator rights.** Datchik asks for them on start. Both the hardware driver and Windows' frame event tracing require them.

## Building and running

```
git clone <repository-url>
cd SysMonitor
dotnet build
```

Then run `SysMonitor.App` from Visual Studio (F5) or from an **administrator** terminal:

```
dotnet run --project SysMonitor.App
```

If Visual Studio offers to restart as administrator, accept it.

## How it works

### Hardware sensors

Sensors are read with [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor).
- **CPU and motherboard values** go through the PawnIO driver.
- **GPU values** come from NVIDIA's own driver.
- **RAM** is read directly from Windows (`GlobalMemoryStatusEx`), the same number Task Manager shows.

### FPS and 1% low

Every time a game finishes a frame, it calls a "Present" function to hand the frame to Windows. Windows reports these calls through **ETW** (Event Tracing for Windows). Datchik listens with [TraceEvent](https://github.com/microsoft/perfview/tree/main/src/TraceEvent) and records a timestamp for every frame.

- **FPS** is the number of frames presented in the last second.
- **1% low** is the 99th-percentile frame time over the last 30 seconds, converted to FPS. It answers: "how low does the frame rate drop during stutters?"

The game is chosen automatically: the foreground window, as long as it renders frames. Clicking on Datchik or another non-game window keeps the previous game selected, so it works with the game on another monitor.

## Project structure

```
SysMonitor.slnx
├─ Spikes/                      Early experiments, kept for reference
│  ├─ FpsSpike                  Console FPS measurement prototype
│  └─ SensorSpike               Console sensor reader
├─ SysMonitor.App/              The WPF app (UI only)
│  ├─ WindowStyling/            Dark title bar, title bar icon removal
│  ├─ App.xaml
│  └─ MainWindow.xaml(.cs)      The main window
└─ SysMonitor.Core/             Data collection, no UI
   ├─ FrameRate/                ETW frame tracking, FPS and 1% low calculation
   ├─ Hardware/                 Sensor reading, sensor mapping, RAM
   └─ Processes/                Foreground process and process name lookups
```

`SysMonitor.Core` has no UI dependencies, so it could be reused, for example by a future overlay.

## Known limitations

- **DirectX only.** FPS tracking covers DirectX 9–12 games. Vulkan and OpenGL games aren't detected yet.
- **NVIDIA only.** The GPU sensors are mapped for NVIDIA cards.
- **Hardware-specific sensor names.** The sensor mapping in `KnownSensors.cs` was made on one PC (Ryzen 7 5700X, ASRock B450M Pro4-F, RTX 4070 SUPER). Other CPUs and motherboards may name their sensors differently. The CPU fan in particular depends on which motherboard header the cooler is plugged into.
- **FPS updates follow Windows' batches.** ETW delivers frame events in batches of about one second, so FPS can't be fresher than that.

## Roadmap

- [ ] Persisted settings: display mode, CPU fan selection, window position
- [ ] App icon and packaging as a single `.exe`
- [ ] Start with Windows without a UAC prompt (Task Scheduler)
- [ ] Configurable sensor mapping for other hardware
- [ ] Vulkan / OpenGL FPS support (e.g. via PresentMon)
- [ ] Optional in-game overlay

## Acknowledgements

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor): hardware sensor access
- [PawnIO](https://pawnio.eu/): signed driver for low-level hardware access
- [TraceEvent](https://github.com/microsoft/perfview/tree/main/src/TraceEvent): Microsoft's library for reading ETW events
- [PresentMon](https://github.com/GameTechDev/PresentMon): the reference for measuring frame rates through ETW
