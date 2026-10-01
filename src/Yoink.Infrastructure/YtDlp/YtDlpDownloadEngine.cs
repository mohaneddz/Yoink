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
    private readonly ILogger<YtDlpDownloadEngine> _logger;

    public YtDlpDownloadEngine(ProcessRunner runner, IToolProvisioner tools, ILogger<YtDlpDownloadEngine> logger)
    {
        _runner = runner;
        _tools = tools;
        _logger = logger;
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
        var args = BuildArguments(request, pathCaptureFile);

        var stage = DownloadStage.Preparing;
        string? lastDestination = null;
        string? lastErrorLine = null;

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
            progress.Report(new DownloadProgress { Stage = DownloadStage.Preparing, RawLine = "Starting download…" });

            var exit = await _runner.RunStreamingAsync(
                _tools.Paths.YtDlpPath,
                args,
                request.OutputFolder,
                line => HandleLine(line, false),
                line => HandleLine(line, true),
                ct).ConfigureAwait(false);

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
        }
    }

    private List<string> BuildArguments(DownloadRequest r, string pathCaptureFile)
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
            "--user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
            "--socket-timeout", "30",
            "--extractor-args", "tiktok:app_info=normal",
            "--format-sort", "hasvid,ie_pref,res,fps",
            "--retries", "10",
            "--fragment-retries", "10",
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

        if (!string.IsNullOrWhiteSpace(r.Proxy))
        {
            args.Add("--proxy");
            args.Add(r.Proxy);
        }

        if (r.SpeedLimitKbps > 0)
        {
            args.Add("--limit-rate");
            args.Add($"{r.SpeedLimitKbps}K");
        }

        args.Add(r.Url.Trim());
        return args;
    }

    private static string BuildVideoSelector(DownloadRequest r)
    {
        if (!string.IsNullOrWhiteSpace(r.FormatId))
        {
            var audioSelector = NormalizeVideoContainer(r.VideoContainer) switch
            {
                "webm" => $"{r.FormatId}+ba[ext=webm]/{r.FormatId}+ba/{r.FormatId}",
                "mp4" => $"{r.FormatId}+ba[ext=m4a]/{r.FormatId}+ba/{r.FormatId}",
                _ => $"{r.FormatId}+ba/{r.FormatId}"
            };
            return audioSelector;
        }

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
