using System.Globalization;
using Microsoft.Extensions.Logging;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Infrastructure.Processes;
using Yoink.Shared;

namespace Yoink.Infrastructure.Ytdlp;

/// <summary>Drives yt-dlp to perform a download, parsing its output into live progress.</summary>
public sealed class YtDlpDownloadEngine : IDownloadEngine
{
    private const string ProgressPrefix = "SVP|";

    private readonly ProcessRunner _runner;
    private readonly IToolProvisioner _tools;
    private readonly IProxyResolver _proxyResolver;
    private readonly ILogger<YtDlpDownloadEngine> _logger;
    private readonly ISettingsService _settings;
    private readonly PreparedDownloadCache _prepared;

    public YtDlpDownloadEngine(ProcessRunner runner, IToolProvisioner tools, IProxyResolver proxyResolver,
        ISettingsService settings, PreparedDownloadCache prepared, ILogger<YtDlpDownloadEngine> logger)
    {
        _runner = runner;
        _tools = tools;
        _proxyResolver = proxyResolver;
        _logger = logger;
        _settings = settings;
        _prepared = prepared;
    }

    public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        var ensure = await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
        if (ensure.IsFailure)
        {
            return new DownloadResult { Outcome = DownloadOutcome.Failed, ErrorMessage = ensure.Error, ExitCode = -1 };
        }

        Directory.CreateDirectory(request.OutputFolder);
        var pathCaptureFile = Path.Combine(Path.GetTempPath(), $"sv-{Guid.NewGuid():N}.path");
        var proxy = !string.IsNullOrWhiteSpace(request.Proxy)
            ? request.Proxy
            : await _proxyResolver.ResolveAsync(request.Url, ct).ConfigureAwait(false);
        string? preparedInfoFile = null;

        var stage = DownloadStage.Preparing;
        string? lastDestination = null;
        string? lastErrorLine = null;
        var transferStarted = false;

        void HandleLine(string line, bool isError)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (isError || line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
            {
                lastErrorLine = line.Trim();
            }

            if (line.StartsWith(ProgressPrefix, StringComparison.Ordinal))
            {
                transferStarted = true;
                var p = ParseProgress(line, stage);
                if (p is not null)
                {
                    progress.Report(p);
                }

                return;
            }

            var newStage = DetectStage(line, request.Kind, stage);
            if (newStage != stage)
            {
                stage = newStage;
                transferStarted = true;
            }

            var dest = ExtractDestination(line);
            if (dest is not null)
            {
                lastDestination = dest;
            }

            progress.Report(DownloadProgress.Log(line, stage));
        }

        try
        {
            if (_settings.Current.ReuseRecentInspection && (request.Kind is DownloadKind.Video or DownloadKind.Audio)
                && _prepared.Get(request.Url, request.Proxy, request.CookiesFile) is { } json)
            {
                preparedInfoFile = Path.Combine(Path.GetTempPath(), $"yoink-{Guid.NewGuid():N}.info.json");
                await File.WriteAllTextAsync(preparedInfoFile, json, ct).ConfigureAwait(false);
            }
            var args = BuildArguments(request, pathCaptureFile, proxy, preparedInfoFile);
            progress.Report(new DownloadProgress { Stage = DownloadStage.Preparing,
                RawLine = preparedInfoFile is not null ? "Using recently inspected streams…" : "Fetching download links…" });

            Task<int> RunFreshAsync() => PreparationRetry.RunAsync(async () =>
            {
                lastErrorLine = null;
                return await _runner.RunStreamingAsync(_tools.Paths.YtDlpPath,
                    BuildArguments(request, pathCaptureFile, proxy), request.OutputFolder,
                    line => HandleLine(line, false), line => HandleLine(line, true), ct).ConfigureAwait(false);
            }, exitCode => exitCode != 0 && !transferStarted && stage == DownloadStage.Preparing
                && PreparationRetry.IsTransient(lastErrorLine), ct,
                attempt => progress.Report(DownloadProgress.Log(
                    $"Connection interrupted; retrying preparation ({attempt}/3)…", DownloadStage.Preparing)));

            var exit = preparedInfoFile is null
                ? await RunFreshAsync().ConfigureAwait(false)
                : await _runner.RunStreamingAsync(_tools.Paths.YtDlpPath, args, request.OutputFolder,
                    line => HandleLine(line, false), line => HandleLine(line, true), ct).ConfigureAwait(false);

            if (exit != 0 && preparedInfoFile is not null)
            {
                ct.ThrowIfCancellationRequested();
                _prepared.Remove(request.Url, request.Proxy, request.CookiesFile);
                lastErrorLine = null;
                transferStarted = false;
                stage = DownloadStage.Preparing;
                progress.Report(DownloadProgress.Log("Refreshing download links and retrying…", stage));
                // Signed URLs can expire sooner than expected. Fall back once to normal extraction.
                exit = await RunFreshAsync().ConfigureAwait(false);
            }

            var finalPath = ResolveFinalPath(pathCaptureFile, lastDestination);
            var size = finalPath is not null && File.Exists(finalPath) ? new FileInfo(finalPath).Length : 0;

            if (exit == 0)
            {
                progress.Report(new DownloadProgress { Stage = DownloadStage.Finished, Percent = 100, Destination = finalPath });
                return new DownloadResult
                {
                    Outcome = DownloadOutcome.Success,
                    ExitCode = 0,
                    OutputPath = finalPath,
                    FileSizeBytes = size
                };
            }

            return new DownloadResult
            {
                Outcome = DownloadOutcome.Failed,
                ExitCode = exit,
                OutputPath = finalPath,
                FileSizeBytes = size,
                ErrorMessage = lastErrorLine ?? $"yt-dlp exited with code {exit}"
            };
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult { Outcome = DownloadOutcome.Cancelled, ExitCode = -1 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed for {Url}", request.Url);
            return new DownloadResult { Outcome = DownloadOutcome.Failed, ExitCode = -1, ErrorMessage = ex.Message };
        }
        finally
        {
            try { if (File.Exists(pathCaptureFile)) File.Delete(pathCaptureFile); } catch { /* best effort */ }
            try { if (preparedInfoFile is not null) File.Delete(preparedInfoFile); } catch { /* best effort */ }
        }
    }

    internal List<string> BuildArguments(DownloadRequest r, string pathCaptureFile, string? proxy, string? preparedInfoFile = null)
    {
        var ffmpegDir = Path.GetDirectoryName(_tools.Paths.FfmpegPath);
        var outputTemplate = string.IsNullOrWhiteSpace(r.FilenameTemplate) ? "%(title)s [%(id)s].%(ext)s" : r.FilenameTemplate;

        var args = new List<string>
        {
            "--no-warnings",
            "--no-playlist",
            "--newline",
            "--no-colors",
            "--windows-filenames",
            "--no-simulate",
            "--continue",
            "--progress-delta", "0.25",
            "--concurrent-fragments", EffectiveFragmentCount(_settings.Current.ConcurrentFragments, proxy).ToString(CultureInfo.InvariantCulture),
            "--buffer-size", "64K",
            "--socket-timeout", "30",
            "--extractor-args", "tiktok:app_info=normal",
            "--format-sort", "hasvid,ie_pref,res,fps",
            "--retries", preparedInfoFile is null ? "10" : "1",
            "--fragment-retries", preparedInfoFile is null ? "10" : "1",
            "--file-access-retries", "5",
            "--progress-template",
            $"download:{ProgressPrefix}%(progress.status)s|%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|%(progress.speed)s|%(progress.eta)s",
            "--print-to-file", "after_move:filepath", pathCaptureFile,
            "-P", r.OutputFolder,
            "-o", outputTemplate
        };

        if (!string.IsNullOrEmpty(ffmpegDir))
        {
            args.Add("--ffmpeg-location");
            args.Add(ffmpegDir);
        }

        switch (r.Kind)
        {
            case DownloadKind.Audio:
                args.Add("-f");
                args.Add(string.IsNullOrWhiteSpace(r.FormatId) ? "ba/b" : r.FormatId);
                args.Add("-x");
                args.Add("--audio-format");
                args.Add(NormalizeAudioContainer(r.AudioContainer));
                args.Add("--audio-quality");
                args.Add(string.IsNullOrWhiteSpace(r.AudioQuality) ? "0" : r.AudioQuality);
                break;

            case DownloadKind.Subtitles:
                args.Add("--skip-download");
                args.Add("--write-subs");
                args.Add("--write-auto-subs");
                args.Add("--sub-langs");
                args.Add(string.IsNullOrWhiteSpace(r.SubtitleLanguages) ? "en.*" : r.SubtitleLanguages);
                args.Add("--convert-subs");
                args.Add(NormalizeSubtitleFormat(r.SubtitleFormat));
                break;

            case DownloadKind.Thumbnail:
                args.Add("--skip-download");
                args.Add("--write-thumbnail");
                break;

            default: // Video
                args.Add("-f");
                args.Add(BuildVideoSelector(r));
                args.Add("--merge-output-format");
                args.Add(NormalizeVideoContainer(r.VideoContainer));
                break;
        }

        if (r.Kind == DownloadKind.Video)
        {
            if (r.EmbedSubtitles)
            {
                args.Add("--embed-subs");
                args.Add("--sub-langs");
                args.Add(string.IsNullOrWhiteSpace(r.SubtitleLanguages) ? "en.*" : r.SubtitleLanguages);
            }

            if (r.EmbedThumbnail)
            {
                args.Add("--embed-thumbnail");
            }

            if (r.WriteThumbnail)
            {
                args.Add("--write-thumbnail");
            }
        }

        if (r.EmbedMetadata && r.Kind is DownloadKind.Video or DownloadKind.Audio)
        {
            args.Add("--embed-metadata");
        }

        if (r.SplitChapters)
        {
            args.Add("--split-chapters");
        }

        if (!string.IsNullOrWhiteSpace(r.CookiesFile) && File.Exists(r.CookiesFile))
        {
            args.Add("--cookies");
            args.Add(r.CookiesFile);
        }

        var effectiveProxy = !string.IsNullOrWhiteSpace(r.Proxy) ? r.Proxy : proxy;
        if (!string.IsNullOrWhiteSpace(effectiveProxy))
        {
            args.Add("--proxy");
            args.Add(effectiveProxy);
        }

        if (r.SpeedLimitKbps > 0)
        {
            args.Add("--limit-rate");
            args.Add($"{r.SpeedLimitKbps}K");
        }

        if (preparedInfoFile is not null)
        {
            args.Add("--load-info-json");
            args.Add(preparedInfoFile);
        }
        else
        {
            args.Add(r.Url.Trim());
        }
        return args;
    }

    internal static int EffectiveFragmentCount(int requested, string? proxy) =>
        Math.Clamp(requested, 1, proxy?.StartsWith("socks", StringComparison.OrdinalIgnoreCase) == true ? 4 : 16);

    private static string BuildVideoSelector(DownloadRequest r)
    {
        var fallback = BuildAutomaticVideoSelector(r);
        if (!string.IsNullOrWhiteSpace(r.FormatId))
        {
            var audioSelector = NormalizeVideoContainer(r.VideoContainer) switch
            {
                "webm" => $"{r.FormatId}+ba[ext=webm]/{r.FormatId}+ba/{r.FormatId}",
                "mp4" => $"{r.FormatId}+ba[ext=m4a]/{r.FormatId}+ba/{r.FormatId}",
                _ => $"{r.FormatId}+ba/{r.FormatId}"
            };
            // Stream IDs can change when an extractor refreshes a manifest. Keep the
            // explicit pick first, then reselect using the requested quality and codec.
            return $"{audioSelector}/{fallback}";
        }

        return fallback;
    }

    private static string BuildAutomaticVideoSelector(DownloadRequest r)
    {
        var height = r.MaxHeight > 0 ? $"[height<=?{r.MaxHeight}]" : string.Empty;
        var codec = MapVideoCodec(r.VideoCodecPreference);
        var codecClause = codec is null ? string.Empty : $"[vcodec^={codec}]";
        const string noWmk = "[format_note!*=?watermark][format_id!*=?download][format_id!*=?wmk]";

        // Prefer clean/unwatermarked streams with codec+height, then relax clean streams, then fallback to standard streams
        return $"bv*{noWmk}{height}{codecClause}+ba/bv*{noWmk}{height}+ba/b{noWmk}{height}/bv*{height}{codecClause}+ba/bv*{height}+ba/b{height}/b";
    }

    private static string? MapVideoCodec(string? pref)
    {
        if (string.IsNullOrWhiteSpace(pref))
        {
            return null;
        }

        var p = pref.ToLowerInvariant();
        if (p.Contains("av1")) return "av01";
        if (p.Contains("vp9")) return "vp09";
        if (p.Contains("265") || p.Contains("hevc")) return "hev1";
        if (p.Contains("264") || p.Contains("avc")) return "avc1";
        return null;
    }

    private static string NormalizeVideoContainer(string c) =>
        string.IsNullOrWhiteSpace(c) ? "mp4" : c.Trim().TrimStart('.').ToLowerInvariant();

    private static string NormalizeAudioContainer(string c)
    {
        var v = string.IsNullOrWhiteSpace(c) ? "m4a" : c.Trim().TrimStart('.').ToLowerInvariant();
        return v switch { "m4a" or "mp3" or "opus" or "aac" or "flac" or "wav" or "vorbis" => v, _ => "m4a" };
    }

    private static string NormalizeSubtitleFormat(string format)
    {
        var value = string.IsNullOrWhiteSpace(format) ? "srt" : format.Trim().TrimStart('.').ToLowerInvariant();
        return value is "srt" or "vtt" or "ass" ? value : "srt";
    }

    private static DownloadProgress? ParseProgress(string line, DownloadStage stage)
    {
        var body = line[ProgressPrefix.Length..];
        var parts = body.Split('|');
        if (parts.Length < 6)
        {
            return null;
        }

        var status = parts[0];
        var downloaded = ParseLong(parts[1]);
        var total = ParseLong(parts[2]) ?? ParseLong(parts[3]);
        var speed = ParseDouble(parts[4]);
        var eta = ParseDouble(parts[5]);

        double? percent = downloaded is { } d && total is { } t && t > 0
            ? Math.Clamp(d * 100.0 / t, 0, 100)
            : null;

        var reportedStage = status == "finished" && stage == DownloadStage.DownloadingVideo
            ? DownloadStage.DownloadingVideo
            : stage;

        return new DownloadProgress
        {
            Stage = reportedStage,
            Percent = percent,
            DownloadedBytes = downloaded,
            TotalBytes = total,
            SpeedBytesPerSecond = speed,
            EtaSeconds = eta
        };
    }

    private static DownloadStage DetectStage(string line, DownloadKind kind, DownloadStage current)
    {
        if (line.Contains("[Merger]", StringComparison.Ordinal)) return DownloadStage.Merging;
        if (line.Contains("[ExtractAudio]", StringComparison.Ordinal)) return DownloadStage.Converting;
        if (line.Contains("[VideoConvertor]", StringComparison.Ordinal) || line.Contains("[VideoRemuxer]", StringComparison.Ordinal)) return DownloadStage.Converting;
        if (line.Contains("[Metadata]", StringComparison.Ordinal) || line.Contains("[EmbedSubtitle]", StringComparison.Ordinal)) return DownloadStage.WritingMetadata;
        if (line.Contains("[EmbedThumbnail]", StringComparison.Ordinal) || line.Contains("[ThumbnailsConvertor]", StringComparison.Ordinal) || line.Contains("Writing video thumbnail", StringComparison.OrdinalIgnoreCase)) return DownloadStage.EmbeddingThumbnail;
        if (line.Contains("Writing video subtitles", StringComparison.OrdinalIgnoreCase) || line.Contains("[SubtitlesConvertor]", StringComparison.Ordinal)) return DownloadStage.DownloadingSubtitles;

        if (line.Contains("[download] Destination:", StringComparison.Ordinal))
        {
            return kind == DownloadKind.Audio ? DownloadStage.DownloadingAudio : DownloadStage.DownloadingVideo;
        }

        return current;
    }

    private static string? ExtractDestination(string line)
    {
        const string marker = "[download] Destination: ";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx >= 0)
        {
            return line[(idx + marker.Length)..].Trim();
        }

        const string alreadyDownloaded = "[download] ";
        const string hasAlready = " has already been downloaded";
        if (line.StartsWith(alreadyDownloaded, StringComparison.Ordinal) && line.Contains(hasAlready, StringComparison.Ordinal))
        {
            var rest = line[alreadyDownloaded.Length..];
            var end = rest.IndexOf(hasAlready, StringComparison.Ordinal);
            if (end >= 0)
            {
                return rest[..end].Trim();
            }
        }

        const string merge = "Merging formats into \"";
        idx = line.IndexOf(merge, StringComparison.Ordinal);
        if (idx >= 0)
        {
            var rest = line[(idx + merge.Length)..];
            var end = rest.IndexOf('"');
            return end >= 0 ? rest[..end] : rest;
        }

        const string thumb = "Writing video thumbnail";
        const string toMarker = " to: ";
        if (line.Contains(thumb, StringComparison.OrdinalIgnoreCase))
        {
            idx = line.IndexOf(toMarker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                return line[(idx + toMarker.Length)..].Trim();
            }
        }

        const string subs = "Writing video subtitles to: ";
        idx = line.IndexOf(subs, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            return line[(idx + subs.Length)..].Trim();
        }

        const string audioDest = "[ExtractAudio] Destination: ";
        idx = line.IndexOf(audioDest, StringComparison.Ordinal);
        if (idx >= 0)
        {
            return line[(idx + audioDest.Length)..].Trim();
        }

        return null;
    }

    private static string? ResolveFinalPath(string captureFile, string? fallback)
    {
        try
        {
            if (File.Exists(captureFile))
            {
                var last = File.ReadAllLines(captureFile).LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (!string.IsNullOrWhiteSpace(last))
                {
                    return last.Trim();
                }
            }
        }
        catch
        {
            // fall through to the parsed destination
        }

        return fallback;
    }

    private static long? ParseLong(string s) =>
        long.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static double? ParseDouble(string s) =>
        double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
}
