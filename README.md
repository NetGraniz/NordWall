<p align="center">
  <img src="Nordwall/assets/nordwall.png" alt="Nordwall logo" width="120">
</p>

<h1 align="center">Nordwall</h1>

<p align="center">
  <strong>Lightweight animated wallpapers for Windows.</strong><br>
  ASCII art, HTML animations, MP4 and WebM — with multi-monitor support.
</p>

<p align="center">
  <a href="https://github.com/NetGraniz/NordWall/releases/latest"><strong>Download Nordwall</strong></a>
  &nbsp;·&nbsp;
  <a href="https://github.com/NetGraniz/NordWall/releases/tag/v0.4.0">v0.4.0 Release</a>
  &nbsp;·&nbsp;
  <a href="https://github.com/NetGraniz/NordWall/issues">Issues &amp; feedback</a>
</p>

<p align="center">
  <img src="Nordwall/assets/example.gif" alt="Animated wallpaper example" width="860">
</p>

<p align="center"><sub>Preview animation: <a href="https://www.youtube.com/watch?v=gU4vSEZwiyE">original video source</a>.</sub></p>

---

Nordwall is a small, open-source animated wallpaper engine for **Windows 11**. It renders animations behind desktop icons, keeps your wallpapers in a regular folder, and stays out of the way in the system tray. It is designed to be lightweight, although actual CPU and GPU usage depends on the wallpaper and your hardware.

## Features

**Animated content.** Run local ASCII/HTML wallpapers (including custom CSS and JavaScript), or play muted, looping MP4 and WebM videos.

**Multiple displays.** Choose independent wallpapers per monitor or a single animation stretched across all connected screens.

**Smart playback.** Automatically suspend covered wallpapers when a fullscreen or maximized app opens, or pause everything manually from the system tray.

**Easy to use.** Manage wallpapers and optional Windows autostart from one Settings window. The published single-file EXE includes its own .NET runtime and internal assets.

### Multi-monitor modes

| Mode | Wallpaper layout | Automatic pause behavior |
| --- | --- | --- |
| **Separate wallpapers** | Select a wallpaper for each screen. Every screen has its own renderer. | Pauses only the screen covered by a fullscreen or maximized app. |
| **Span across displays** | One continuous wallpaper covers the entire Windows virtual desktop. A single renderer keeps it synchronized. | Pauses the entire wallpaper when a fullscreen or maximized app appears on any screen. |

The **Settings** window contains a visual map of connected monitors. Click a display to assign its wallpaper. Nordwall reads monitor positions and resolutions from Windows, including screens placed to the left of or above the primary display.

To rearrange monitors, use **Arrange displays…** in Nordwall Settings. This opens Windows Display Settings; changes are picked up automatically. The miniature display map is a selector, not a drag-and-drop layout editor.

You can disable automatic fullscreen pausing in Settings. Manual **Pause / Resume** in the tray affects all displays.

## Download and installation

Download **`Nordwall.exe`** from the [latest GitHub release](https://github.com/NetGraniz/NordWall/releases/latest) and run it. No installer or .NET SDK is required to use the published EXE.

Nordwall uses **Microsoft Edge WebView2 Runtime** to display HTML and video. It is typically already installed on Windows 11. If Nordwall reports that WebView2 is missing, install the [official WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) and try again.

At first launch, Nordwall creates an empty **`Documents\Wallpapers`** folder, respecting your Windows Documents location (including redirected folders). It never silently installs, restores or replaces your wallpaper files.

Download the optional **`Wallpapers.2.0.rar`** pack from the [v0.4.0 release](https://github.com/NetGraniz/NordWall/releases/tag/v0.4.0), or add your own `.html`, `.htm`, `.mp4` and `.webm` files directly to `Documents\Wallpapers`. Then right-click the Nordwall tray icon, open **Settings**, select a monitor or Span mode, and choose the wallpaper you want. Reopen Settings after adding files to refresh the list.

The wallpapers are distributed separately from the application. You can add or remove them freely without rebuilding Nordwall.

## Using Nordwall

Right-click the system tray icon to access **Pause / Resume**, **Open wallpapers folder**, and **Settings**. Double-clicking the tray icon also opens Settings.

Settings contains the wallpaper picker, display mode, fullscreen auto-pause, and **Launch Nordwall at sign-in** option. Use **Exit Nordwall** in Settings to close the program.

Autostart is **off by default**. When enabled, Nordwall adds an entry under the current Windows user's `Run` registry key. If you move `Nordwall.exe` to a different location, update the autostart setting afterwards.

## Included ASCII wallpapers

The source repository includes ten example ASCII animations in [`Nordwall/wallpapers`](Nordwall/wallpapers). These are examples for developers, **not** wallpapers automatically installed by the EXE.

| Original collection | Second collection |
| --- | --- |
| Orbit | Tidal |
| Waves | Matrix Rain |
| Tunnel | Nocturne |
| Aurora | Pulse |
| Galaxy | Ember |

All ten examples are self-contained HTML files with precomputed animation frames and seamless looping motion. For video wallpapers, playback support depends on the codecs available in your installed WebView2 Runtime.

## Build from source

To build Nordwall, install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) on Windows.

Clone or download the repository, then double-click **`run.cmd`** to build and launch Nordwall. You can also run it from a terminal:

```powershell
dotnet run --project Nordwall/Nordwall.csproj --configuration Release
```

To generate a self-contained Windows x64 EXE, run **`publish.cmd`**. The result is written to:

```text
release/
  Nordwall-standalone-win-x64/
    Nordwall.exe
  Nordwall-standalone-win-x64.zip
```

The publish command bundles .NET and the application assets into the EXE. The separate **WebView2 Runtime** is still required on the destination machine. The example wallpapers are not embedded or copied into the release output.

## Compatibility and limitations

Nordwall is developed and tested on **Windows 11**. Windows 10 compatibility is not guaranteed and needs additional testing. Multi-display setups with mixed resolutions or DPI scaling may also require testing on individual systems.

To draw behind desktop icons, Nordwall uses the undocumented Windows `Progman` / `WorkerW` desktop layout. Windows updates can change this behavior. The monitor map follows the arrangement configured in Windows; it does not change the actual display topology.

Nordwall is intended for local, offline wallpapers. Its WebView2 host blocks external network requests from loaded wallpaper pages, but you should still use HTML wallpapers from sources you trust.

Found a bug or have an idea? [Open an issue](https://github.com/NetGraniz/NordWall/issues).

## License and credits

Nordwall's source code is released under the [MIT License](LICENSE).

The README preview GIF is credited to its [original animation source](https://www.youtube.com/watch?v=gU4vSEZwiyE). Other media retain their respective creators' rights.
