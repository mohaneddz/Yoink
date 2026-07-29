# Yoink

A modern Windows desktop media downloader built on **yt-dlp** and **FFmpeg**, with a clean WPF interface for downloading, organizing, and managing video and audio from thousands of sites.

Yoink provisions its own copies of yt-dlp and FFmpeg on first launch, so there is nothing to install separately — download the app and go.

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

To distribute, ship that single `Yoink.exe`. On first launch it creates its data folder,
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
