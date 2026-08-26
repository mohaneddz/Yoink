![Yoink](screenshots/cover.avif)

<h1 style="font-family: Arial, sans-serif; font-size: 36px; color: #f96a1c; display: flex; align-items: center; gap: 12px; border-bottom: 3px solid #f96a1c; padding-bottom: 8px;">
  Yoink — Media Downloader for Windows
</h1>

**Yoink** is a medvia downloader for Windows that wraps the power of **yt-dlp** and **FFmpeg** in a clean, native WPF app — grab video or audio from thousands of sites, then keep it all organized in one place.

Point it at a URL, pick your quality and format, and go. Yoink handles playlists, follows channels for new uploads, queues concurrent downloads, and keeps a searchable library and history of everything you've saved — no command line, no browser extensions, no ads.

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

- **Download** — paste a URL, inspect available streams, pick quality/format, and download video or audio.
- **Queue** — manage concurrent downloads with pause/resume/retry and live progress.
- **Playlists** — expand and selectively download playlist entries.
- **Channels** — follow channels and browse/download their uploads.
- **Library** — browse everything you've downloaded, filtered by media type.
- **History** — full record of past downloads with re-download and export.
- **Settings** — configure download folder, concurrency, speed limits, preferred containers/quality,
  filename templates, proxy, cookies, tool paths, appearance, and diagnostics.

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

- **"Tools unavailable" on startup** — Yoink couldn't download yt-dlp/FFmpeg. Check your
  internet connection, or set explicit executable paths in **Settings → Integrations**.
- **Logs** — open **Settings → Advanced → View Logs** (or browse to
  `%LocalAppData%\Yoink\logs`). Enable **Diagnostic logging** for verbose output.

## License

This project bundles/downloads third-party tools that carry their own licenses:
[yt-dlp](https://github.com/yt-dlp/yt-dlp) (Unlicense) and
[FFmpeg](https://ffmpeg.org/legal.html) (GPL/LGPL builds from yt-dlp/FFmpeg-Builds).
