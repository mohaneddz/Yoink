using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StreamVault.Application.Abstractions;
using StreamVault.Domain.Media;
using StreamVault.Infrastructure.Processes;
using StreamVault.Shared;

namespace StreamVault.Infrastructure.Ytdlp;

/// <summary>Inspects URLs with yt-dlp (<c>-J</c>) and maps the JSON to domain metadata.</summary>
public sealed partial class YtDlpInspector : IMediaInspector
{
    private readonly ProcessRunner _runner;
    private readonly IToolProvisioner _tools;
    private readonly ILogger<YtDlpInspector> _logger;

    public YtDlpInspector(ProcessRunner runner, IToolProvisioner tools, ILogger<YtDlpInspector> logger)
    {
        _runner = runner;
        _tools = tools;
        _logger = logger;
    }

    public MediaSourceKind DetectKind(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
        {
            return MediaSourceKind.Unknown;
        }

        var u = url.Trim();
        if (PlaylistRegex().IsMatch(u))
        {
            return MediaSourceKind.Playlist;
        }

        if (ShortsRegex().IsMatch(u))
        {
            return MediaSourceKind.Short;
        }

        if (ChannelRegex().IsMatch(u))
        {
            return MediaSourceKind.Channel;
        }

        if (VideoRegex().IsMatch(u))
        {
            return MediaSourceKind.Video;
        }

        return MediaSourceKind.Video; // default: attempt as a single video
    }

    public async Task<Result<MediaMetadata>> InspectAsync(string url, CancellationToken ct = default)
    {
        var ensure = await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
        if (ensure.IsFailure)
        {
            return Result<MediaMetadata>.Failure(ensure.Error!);
        }

        var args = new List<string>
        {
            "-J", "--no-warnings", "--no-playlist", "--no-progress", url.Trim()
        };

        try
        {
            var result = await _runner.RunCaptureAsync(_tools.Paths.YtDlpPath, args, ct).ConfigureAwait(false);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Result<MediaMetadata>.Failure(FirstError(result.StandardError) ?? "Inspection failed.");
            }

            using var doc = JsonDocument.Parse(result.StandardOutput);
            return Result<MediaMetadata>.Success(MapMedia(doc.RootElement, url));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inspect failed for {Url}", url);
            return Result<MediaMetadata>.Failure(ex.Message);
        }
    }

    public async Task<Result<PlaylistMetadata>> InspectPlaylistAsync(string url, int? limit = null, CancellationToken ct = default)
    {
        var ensure = await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
        if (ensure.IsFailure)
        {
            return Result<PlaylistMetadata>.Failure(ensure.Error!);
        }

        var args = new List<string>
        {
            "-J", "--flat-playlist", "--no-warnings", "--no-progress"
        };
        if (limit is > 0)
        {
            args.Add("--playlist-items");
            args.Add($"1:{limit}");
        }

        args.Add(url.Trim());

        try
        {
            var result = await _runner.RunCaptureAsync(_tools.Paths.YtDlpPath, args, ct).ConfigureAwait(false);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Result<PlaylistMetadata>.Failure(FirstError(result.StandardError) ?? "Playlist inspection failed.");
            }

            using var doc = JsonDocument.Parse(result.StandardOutput);
            return Result<PlaylistMetadata>.Success(MapPlaylist(doc.RootElement, url));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playlist inspect failed for {Url}", url);
            return Result<PlaylistMetadata>.Failure(ex.Message);
        }
    }

    private static MediaMetadata MapMedia(JsonElement root, string requestedUrl)
    {
        var formats = new List<MediaFormat>();
        if (root.TryGetProperty("formats", out var fmtArray) && fmtArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in fmtArray.EnumerateArray())
            {
                var formatId = f.GetStr("format_id");
                if (string.IsNullOrEmpty(formatId))
                {
                    continue;
                }

                formats.Add(new MediaFormat
                {
                    FormatId = formatId,
                    FormatNote = f.GetStr("format_note"),
                    Extension = f.GetStr("ext"),
                    Resolution = f.GetStr("resolution"),
                    Width = f.GetInt("width"),
                    Height = f.GetInt("height"),
                    Fps = f.GetDouble("fps"),
                    VideoCodec = f.GetStr("vcodec"),
                    AudioCodec = f.GetStr("acodec"),
                    FilesizeBytes = f.GetLong("filesize"),
                    FilesizeApproxBytes = f.GetLong("filesize_approx"),
                    TotalBitrateKbps = f.GetDouble("tbr"),
                    VideoBitrateKbps = f.GetDouble("vbr"),
                    AudioBitrateKbps = f.GetDouble("abr"),
                    AudioChannels = f.GetInt("audio_channels"),
                    DynamicRange = f.GetStr("dynamic_range")
                });
            }
        }

        var subs = ReadLangKeys(root, "subtitles");
        var autoSubs = ReadLangKeys(root, "automatic_captions");
        var chapterCount = root.TryGetProperty("chapters", out var ch) && ch.ValueKind == JsonValueKind.Array ? ch.GetArrayLength() : 0;

        return new MediaMetadata
        {
            Id = root.GetStr("id") ?? string.Empty,
            WebpageUrl = root.GetStr("webpage_url") ?? requestedUrl,
            OriginalUrl = root.GetStr("original_url"),
            Title = root.GetStr("title") ?? "(untitled)",
            Description = root.GetStr("description"),
            Channel = root.GetStr("channel") ?? root.GetStr("uploader"),
            ChannelId = root.GetStr("channel_id"),
            ChannelUrl = root.GetStr("channel_url") ?? root.GetStr("uploader_url"),
            Uploader = root.GetStr("uploader"),
            ChannelFollowerCount = root.GetLong("channel_follower_count"),
            DurationSeconds = root.GetDouble("duration"),
            ViewCount = root.GetLong("view_count"),
            LikeCount = root.GetLong("like_count"),
            UploadDate = root.GetDate("upload_date"),
            ThumbnailUrl = root.GetStr("thumbnail"),
            IsLive = root.GetBool("is_live"),
            WasLive = root.GetBool("was_live"),
            Availability = root.GetStr("availability"),
            Extractor = root.GetStr("extractor_key") ?? root.GetStr("extractor"),
            SourceKind = ClassifyMedia(root),
            Formats = formats,
            SubtitleLanguages = subs,
            AutomaticCaptionLanguages = autoSubs,
            ChapterCount = chapterCount
        };
    }

    private static PlaylistMetadata MapPlaylist(JsonElement root, string requestedUrl)
    {
        var entries = new List<PlaylistEntry>();
        if (root.TryGetProperty("entries", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in arr.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var id = e.GetStr("id") ?? string.Empty;
                var entryUrl = e.GetStr("url") ?? e.GetStr("webpage_url") ?? id;
                entries.Add(new PlaylistEntry
                {
                    Id = id,
                    Url = entryUrl,
                    Title = e.GetStr("title") ?? "(untitled)",
                    DurationSeconds = e.GetDouble("duration"),
                    Uploader = e.GetStr("uploader") ?? e.GetStr("channel"),
                    ViewCount = e.GetLong("view_count"),
                    ThumbnailUrl = e.GetStr("thumbnail"),
                    UploadDate = e.GetDate("upload_date"),
                    Availability = e.GetStr("availability") ?? "available"
                });
            }
        }

        return new PlaylistMetadata
        {
            Id = root.GetStr("id") ?? string.Empty,
            WebpageUrl = root.GetStr("webpage_url") ?? requestedUrl,
            Title = root.GetStr("title") ?? "(untitled)",
            Uploader = root.GetStr("uploader") ?? root.GetStr("channel"),
            ChannelId = root.GetStr("channel_id"),
            Description = root.GetStr("description"),
            Availability = root.GetStr("availability"),
            SourceKind = ClassifyMedia(root),
            Entries = entries
        };
    }

    private static IReadOnlyList<string> ReadLangKeys(JsonElement root, string prop)
    {
        if (root.TryGetProperty(prop, out var el) && el.ValueKind == JsonValueKind.Object)
        {
            return el.EnumerateObject().Select(p => p.Name).ToList();
        }

        return Array.Empty<string>();
    }

    private static MediaSourceKind ClassifyMedia(JsonElement root)
    {
        var type = root.GetStr("_type");
        if (type is "playlist" or "multi_video")
        {
            var extractor = root.GetStr("extractor_key") ?? string.Empty;
            return extractor.Contains("channel", StringComparison.OrdinalIgnoreCase) || extractor.Contains("tab", StringComparison.OrdinalIgnoreCase)
                ? MediaSourceKind.Channel
                : MediaSourceKind.Playlist;
        }

        if (root.GetBool("is_live"))
        {
            return MediaSourceKind.Livestream;
        }

        return MediaSourceKind.Video;
    }

    private static string? FirstError(string stderr)
    {
        var line = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(l => l.Contains("ERROR", StringComparison.OrdinalIgnoreCase));
        return line?.Replace("ERROR:", string.Empty).Trim();
    }

    [GeneratedRegex(@"[?&]list=", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistRegex();

    [GeneratedRegex(@"/shorts/", RegexOptions.IgnoreCase)]
    private static partial Regex ShortsRegex();

    [GeneratedRegex(@"youtube\.com/(@|channel/|c/|user/)", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelRegex();

    [GeneratedRegex(@"(watch\?v=|youtu\.be/|/video/|vimeo\.com/\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VideoRegex();
}
