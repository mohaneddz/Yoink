![Yoink](screenshots/cover.avif)

<h1 style="font-family: Arial, sans-serif; font-size: 36px; color: #f96a1c; display: flex; align-items: center; gap: 12px; border-bottom: 3px solid #f96a1c; padding-bottom: 8px;">
  Yoink — Media Downloader for Windows
</h1>

**Yoink** is a universal media downloader for Windows that wraps the power of **yt-dlp** and **FFmpeg** in a clean, native WPF app — download high-fidelity video or audio from thousands of websites with automatic watermark removal, playlist expansion, and complete library management.

Point it at any URL, pick your quality and container, and go. Yoink automatically selects clean, unwatermarked streams (for TikTok, Instagram, Twitter/X, Facebook, and more), handles playlists and channel feeds, queues concurrent downloads, and keeps a searchable library and history of everything you've saved — no command line, no browser extensions, no ads.

It provisions its own copies of yt-dlp and FFmpeg on first launch, so there is nothing to install separately: download the app and start yoinking.

## Screenshots

### Download

Inspect a URL, choose the quality and format, configure subtitles and metadata, then start the download.

![Yoink download screen](screenshots/download.avif)

### Queue and playlists

Manage active downloads with live progress, or inspect a playlist and selectively queue its entries.

| Queue | Playlists |
|---|---|
| ![Yoink queue screen](screenshots/queue.avif) | ![Yoink playlists screen](screenshots/playlists.avif) |

### Library, channels, history, and settings

Keep downloads organized, follow channels for new uploads, review past activity, and customize the app from one place.

| Library | Channels |
|---|---|
| ![Yoink library screen](screenshots/library.avif) | ![Yoink channels screen](screenshots/channels.avif) |

| History | Settings |
|---|---|
| ![Yoink history screen](screenshots/history.avif) | ![Yoink settings screen](screenshots/settings.avif) |

## Features

- **Universal Download** — paste any media URL, inspect available streams, select quality/format, and download clean video or audio with automatic watermark filtering.
- **Queue** — manage concurrent downloads with pause/resume/retry and live progress.
- **Playlists** — expand and selectively download playlist entries.
- **Channels** — follow channels and browse/download their uploads.
- **Library** — browse everything you've downloaded, filtered by media type.
- **History** — full record of past downloads with re-download and export.
- **Settings & Network** — configure download folder, concurrency, speed limits, preferred containers/quality,
  filename templates, proxy, cookies (for age-restricted or private media), tool paths, appearance, and diagnostics.

### Faster starts and transfers

Fresh inspection results are reused when starting video/audio downloads, avoiding a second webpage lookup. If cached links fail, Yoink automatically fetches fresh links and retries. Prepared download links remain in memory for two minutes.

In **Settings > Downloads**, adjust **Parallel fragments per download** (default: 8) or disable **Quick start using recent inspection**. SOCKS proxy downloads use at most four parallel fragments. These options improve preparation and segmented transfers; actual speed still depends on the source and connection.

### Background operation

Closing the window keeps Yoink running in the system tray by default, including active downloads. Double-click the tray icon to restore the window; right-click it to open Yoink, pause/resume downloads, or explicitly exit. Launching Yoink again restores the existing instance instead of creating another queue owner.

**Settings > General** includes close/minimize-to-tray, start with Windows, start minimized, interrupted-download recovery, notifications, and sleep protection during downloads. Windows startup and interrupted-download recovery are opt-in; enable them and save settings. Manually paused downloads stay paused on startup. Sleep protection allows the display to turn off while downloads continue.

Data (settings, the SQLite database, provisioned tools, and logs) is stored under
`%LocalAppData%\Yoink`.

## Requirements

- **Windows 10/11 (x64)**
- To build from source: **.NET 8 SDK**

The published, self-contained build bundles the .NET runtime, so end users do **not** need
.NET installed. yt-dlp and FFmpeg are downloaded automatically on first run.

## Project structure

Clean-architecture layering:

| Project | Responsibility |
|---|---|
| `Yoink.Domain` | Entities, media/settings models (no dependencies) |
| `Yoink.Application` | Abstractions and use-case contracts |
| `Yoink.Infrastructure` | yt-dlp/FFmpeg integration, SQLite persistence, settings, tool provisioning |
| `Yoink.Presentation` | WPF UI (MVVM via CommunityToolkit.Mvvm) |
| `Yoink.Shared` | Cross-cutting primitives (e.g. `Result`) |

## Building & running (from source)

```bat
:: Restore + build (Debug)
build.bat

:: Run
run.bat
```

Or with the .NET CLI directly:

```bat
dotnet build Yoink.slnx
dotnet run --project src/Yoink.Presentation/Yoink.Presentation.csproj
```

## Tests

```bat
dotnet test Yoink.slnx
```

## Publishing a release build

Produces a single self-contained `Yoink.exe` (no external dependencies):

```bat
publish.bat
```

Output: `src\Yoink.Presentation\bin\Release\net8.0-windows\win-x64\publish\Yoink.exe`

The same profile can be invoked via the CLI:

```bat
dotnet publish src/Yoink.Presentation/Yoink.Presentation.csproj -p:PublishProfile=win-x64
```

To build the Windows Installer (`YoinkSetup.msi`):

```bat
make_installer.bat
```

Output: `dist\YoinkSetup.msi`

To distribute, ship the `YoinkSetup.msi` installer or the standalone `Yoink.exe`. On first launch it creates its data folder,
initializes the database, and downloads yt-dlp/FFmpeg into `%LocalAppData%\Yoink\tools`.

## Troubleshooting

- **Source verification or failed stream discovery** — update the downloader under **Settings > Integrations** and retry. If the source still requires a verified session, configure your cookies file under **Settings > Network**.
- **"Tools unavailable" on startup** — Yoink couldn't download yt-dlp/FFmpeg. Check your
  internet connection, or set explicit executable paths in **Settings → Integrations**.
- **Logs** — open **Settings → Advanced → View Logs** (or browse to
  `%LocalAppData%\Yoink\logs`). Enable **Diagnostic logging** for verbose output.

## License

This project bundles/downloads third-party tools that carry their own licenses:
[yt-dlp](https://github.com/yt-dlp/yt-dlp) (Unlicense) and
[FFmpeg](https://ffmpeg.org/legal.html) (GPL/LGPL builds from yt-dlp/FFmpeg-Builds).
