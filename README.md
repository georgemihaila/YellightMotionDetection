# YellightMotionDetection

A C# WPF (Windows desktop) application that uses webcams to detect motion and its direction, then automatically turns matching Yeelight smart bulbs on or off based on which way a person moved past the camera.

The app enumerates every connected webcam, displays a live camera feed with a motion-detection grid overlay, and continuously analyzes frames with AForge.NET's motion-detection pipeline. When movement is detected, it computes a direction (Left/Right/Up/Down) and consults a JSON binding file (`bindings.json`) that maps a camera + movement direction to a set of Yeelight devices to turn on or off. It discovers Yeelight bulbs on the LAN automatically via the YeelightAPI library.

## Features

- **Multi-camera support** — every video input device is detected at startup and rendered side-by-side in a `UniformGrid`; each camera is identified by the SHA-256 hash of its DirectShow moniker string.
- **Live video view** — each camera view shows the feed at the highest available resolution, plus an on-screen info overlay (resolution, FPS, error count) and automatic restart on video-source errors.
- **Motion detection grid** — AForge `SimpleBackgroundModelingDetector` + `GridMotionAreaProcessing` divides the frame into an N×N grid (default 4×4), highlights motion zones, and draws a line showing the average motion direction over time.
- **Direction tracking** — computes X/Y movement direction (Left/Right/Up/Down) from the centroid of motion zones and fires a `MotionDetected` event; the current direction is shown on screen.
- **Per-camera live tuning** — sliders for detection **Sensitivity** (0.01–0.15) and **grid Size** (2–20); settings are persisted per camera to `<cameraID>_config.json`.
- **Yeelight control** — discovers Yeelight devices with `DeviceLocator.Discover()`, auto-reconnects (with retries), and runs TurnOn/TurnOff commands in parallel for the lights bound to the detected movement direction.
- **Direction-to-light bindings** — `bindings.json` defines, per camera, which Yeelight device IDs are switched on and which are switched off when movement in a given direction is detected.
- **Cool-down period** — a configurable per-camera cool-off (default 2000 ms) prevents repeated triggers while the same movement event persists.
- **Force-quit on close** — the app deliberately terminates the process on close because AForge's capture pipeline can otherwise hang the WPF shutdown.

## Project structure

```
YellightMotionDetection/
├── dependencies/
│   └── YeelightAPI.dll          # Local copy of the YeelightAPI library (PE32 .NET assembly)
├── MotionDetection.sln          # Visual Studio 2019 solution (single project)
└── MotionDetection/
    ├── MotionDetection.csproj   # .NET Core 3.1 WPF project (WinExe)
    ├── App.xaml / App.xaml.cs   # Application entry, global unhandled-exception hook
    ├── MainWindow.xaml(.cs)     # Hosts one MotionView per detected camera; Yeelight discovery;
    │                            #   wires MotionDetected -> HandleCameraMovementAsync
    ├── bindings.json            # CameraID + direction -> Yeelight On/Off bindings (copied to output)
    ├── CameraView.xaml(.cs)     # Simple camera feed user control (AForge VideoCaptureDevice)
    ├── MotionView.xaml(.cs)     # Camera + detection grid + sliders + direction line + config save
    ├── MotionDetectionConfiguration.cs  # Sensitivity + Size settings persisted per camera
    ├── MotionDetectionResult.cs         # Direction tuple + detection time; MovementDirection enum
    ├── ObservableDictionary.cs         # INotifyCollectionChanged/INotifyPropertyChanged dictionary
    ├── PercentageConverter.cs          # WPF IValueConverter (MarkupExtension) for sizing the grid
    ├── AssemblyInfo.cs                 # WPF theme-info assembly attribute
    └── Configuration/
        ├── CameraConfiguration.cs             # Cooloff + CameraBindings
        ├── CameraBinding.cs                   # CameraID + per-direction bindings
        ├── CameraDirectionLightStateBinding.cs# Direction -> On/Off action descriptor
        └── OnOffActionDescriptor.cs           # Lists of Yeelight IDs to turn On / Off
```

## Tech stack

- **Language/framework:** C#, WPF, target framework **`netcoreapp3.1`** (`Microsoft.NET.Sdk.WindowsDesktop`).
- **Build tool:** Visual Studio 2019 (solution format v12, `VisualStudioVersion` 16.x); SDK-style project, so `dotnet` CLI also works.
- **Libraries:**
  - `AForge` 2.2.5, `AForge.Video.DirectShow` 2.2.5, `AForge.Vision` 2.2.5 — video capture and motion detection.
  - `Newtonsoft.Json` 12.0.3 — `bindings.json` / per-camera config serialization.
  - `System.Drawing.Common` 4.7.0 — bitmap frame handling.
  - `YeelightAPI` — external DLL (see note below).
- **Target platform:** Windows (WPF, DirectShow cameras).

> **YeelightAPI reference note:** the `.csproj` references `YeelightAPI` via a `HintPath` pointing outside the repository (`..\..\..\..\Documents\GitHub\YeelightAPI\YeelightAPI\bin\Debug\netstandard2.0\YeelightAPI.dll`). A copy of the assembly is also present in the repo's `dependencies/` folder. To build, either create/point the reference at an available `YeelightAPI.dll` or update the `HintPath` to the bundled `dependencies\YeelightAPI.dll`.

## Build & run

Requires the .NET Core 3.1 SDK (and Windows for WPF).

```bash
# From the repository root
dotnet build MotionDetection.sln

# Run the WPF app
dotnet run --project MotionDetection/MotionDetection.csproj
```

Alternatively, open `MotionDetection.sln` in Visual Studio 2019 and run (F5).

## Usage notes / configuration

- **`bindings.json`** (copied to the output directory on build) drives the camera → light automation:
  ```json
  {
    "Cooloff": 2000,
    "CameraBindings": [
      {
        "CameraID": "e325c54b...",           // SHA-256 of the camera's DirectShow moniker
        "Bindings": [
          { "Direction": "Right",
            "Actions": { "On":  ["0x0000000010f64b7e", "0x0000000010f64f57"],
                         "Off": ["0x00000000129d5ca5"] } },
          { "Direction": "Left",
            "Actions": { "On":  ["0x00000000129d5ca5"],
                         "Off": ["0x0000000010f64b7e", "0x0000000010f64f57"] } }
        ]
      }
    ]
  }
  ```
  - `Cooloff` — milliseconds to wait between triggers for the same camera.
  - `CameraID` — SHA-256 of the camera moniker string (printed in the console at startup).
  - `Direction` — `Up`, `Down`, `Left`, `Right`, or `None`.
  - `Actions.On` / `Actions.Off` — Yeelight device IDs to switch on / off.
- **Per-camera settings** — changing the Sensitivity or Size slider writes `<cameraID>_config.json` (fields `Size`, `Sensitivity`) next to the app, reloaded on startup.
- **Yeelight discovery** — lights must be enabled in Yeelight LAN mode; the app discovers them via the local network (`DeviceLocator.Discover()`) and auto-reconnects up to 3 times if a connection drops.
- The app prints discovery/initialization info to the console (run from a terminal to see it).

## License

No `LICENSE` file is present in the repository.
