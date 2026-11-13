using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Yoink.Application.Abstractions;
using Yoink.Domain.Media;
using Yoink.Infrastructure.Processes;
using Yoink.Shared;

namespace Yoink.Infrastructure.Ytdlp;

/// <summary>Inspects URLs with yt-dlp (<c>-J</c>) and maps the JSON to domain metadata.</summary>
public sealed partial class YtDlpInspector : IMediaInspector
{
    private readonly MediaInspectionCache _cache = new();
    private readonly ProcessRunner _runner;
    private readonly IToolProvisioner _tools;
    private readonly ISettingsService _settings;
    private readonly IProxyResolver _proxyResolver;
    private readonly ILogger<YtDlpInspector> _logger;
    private readonly PreparedDownloadCache _prepared;

    public YtDlpInspector(
        ProcessRunner runner,
        IToolProvisioner tools,
        ISettingsService settings,
        IProxyResolver proxyResolver,
        PreparedDownloadCache prepared,
        ILogger<YtDlpInspector> logger)
    {
        _runner = runner;
        _tools = tools;
        _settings = settings;
        _proxyResolver = proxyResolver;
        _prepared = prepared;
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

    public Task<Result<MediaMetadata>> InspectAsync(string url, CancellationToken ct = default, bool forceRefresh = false)
    {
        var settings = _settings.Current;
        var key = (url.Trim(), settings.Proxy, settings.CookiesFile);
        return _cache.GetAsync(key, token => InspectCoreAsync(url, token), forceRefresh, ct);
    }

    private async Task<Result<MediaMetadata>> InspectCoreAsync(string url, CancellationToken ct)
    {
        var configuredProxy = _settings.Current.Proxy;
        var cookies = _settings.Current.CookiesFile;
        _prepared.Remove(url, configuredProxy, cookies);
        var ensure = await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
        if (ensure.IsFailure)
        {
            return Result<MediaMetadata>.Failure(ensure.Error!);
        }

        var args = new List<string>
        {
            "-J", "--no-warnings", "--no-playlist", "--no-progress",
            "--extractor-args", "tiktok:app_info=normal"
        };
        var proxy = !string.IsNullOrWhiteSpace(_settings.Current.Proxy)
            ? _settings.Current.Proxy
            : await _proxyResolver.ResolveAsync(url, ct).ConfigureAwait(false);
        AppendCommonInspectionArgs(args, proxy);
        args.Add(url.Trim());

        try
        {
            var result = await _runner.RunCaptureAsync(_tools.Paths.YtDlpPath, args, ct).ConfigureAwait(false);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return Result<MediaMetadata>.Failure(FirstError(result.StandardError) ?? "Inspection failed.");
            }

            using var doc = JsonDocument.Parse(result.StandardOutput);
            var media = MapMedia(doc.RootElement, url);
            _prepared.Store(url, configuredProxy, cookies, result.StandardOutput, media);
            return Result<MediaMetadata>.Success(media);
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
            "-J", "--flat-playlist", "--no-warnings", "--no-progress",
            "--extractor-args", "youtubetab:approximate_date",
            "--extractor-args", "tiktok:app_info=normal"
        };
        var playlistProxy = !string.IsNullOrWhiteSpace(_settings.Current.Proxy)
            ? _settings.Current.Proxy
            : await _proxyResolver.ResolveAsync(url, ct).ConfigureAwait(false);
        AppendCommonInspectionArgs(args, playlistProxy);
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
            ThumbnailUrl = root.GetStr("thumbnail") ?? ExtractThumbnail(root),
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
                var thumb = e.GetStr("thumbnail") ?? ExtractThumbnail(e);
                if (string.IsNullOrEmpty(thumb) && !string.IsNullOrEmpty(id) && !id.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    thumb = $"https://i.ytimg.com/vi/{id}/hqdefault.jpg";
                }

                entries.Add(new PlaylistEntry
                {
                    Id = id,
                    Url = entryUrl,
                    Title = e.GetStr("title") ?? "(untitled)",
                    DurationSeconds = e.GetDouble("duration"),
                    Uploader = e.GetStr("uploader") ?? e.GetStr("channel"),
                    ViewCount = e.GetLong("view_count"),
                    ThumbnailUrl = thumb,
                    UploadDate = e.GetDate("upload_date"),
                    Availability = e.GetStr("availability") ?? "available"
                });
            }
        }

        var playlistThumb = root.GetStr("thumbnail") ?? ExtractThumbnail(root) ?? entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.ThumbnailUrl))?.ThumbnailUrl;

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
            ThumbnailUrl = playlistThumb,
            ChannelUrl = root.GetStr("channel_url") ?? root.GetStr("uploader_url"),
            SubscriberCount = root.GetLong("channel_follower_count"),
            Entries = entries
        };
    }

    private static string? ExtractThumbnail(JsonElement el)
    {
        if (el.TryGetProperty("thumbnails", out var thumbs) && thumbs.ValueKind == JsonValueKind.Array)
        {
            string? bestUrl = null;
            var maxRes = -1;
            foreach (var t in thumbs.EnumerateArray())
            {
                var url = t.GetStr("url");
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var width = t.GetInt("width") ?? 0;
                var height = t.GetInt("height") ?? 0;
                var res = width * height;
                if (res > maxRes || bestUrl is null)
                {
                    maxRes = res;
                    bestUrl = url;
                }
            }

            return bestUrl;
        }

        return null;
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

    private void AppendCommonInspectionArgs(List<string> args, string? proxy)
    {
        args.Add("--socket-timeout");
        args.Add("20");
        args.Add("--retries");
        args.Add("3");

        var settings = _settings.Current;
        if (!string.IsNullOrWhiteSpace(settings.CookiesFile) && File.Exists(settings.CookiesFile))
        {
            args.Add("--cookies");
            args.Add(settings.CookiesFile);
        }

        if (!string.IsNullOrWhiteSpace(proxy))
        {
            args.Add("--proxy");
            args.Add(proxy);
        }
    }

    [GeneratedRegex(@"[?&]list=", RegexOptions.IgnoreCase)]
    private static partial Regex PlaylistRegex();

    [GeneratedRegex(@"(/shorts/|/reel/|/reels/|tiktok\.com|vm\.tiktok\.com)", RegexOptions.IgnoreCase)]
    private static partial Regex ShortsRegex();

    [GeneratedRegex(@"youtube\.com/(@|channel/|c/|user/)", RegexOptions.IgnoreCase)]
    private static partial Regex ChannelRegex();

    [GeneratedRegex(@"(watch\?v=|youtu\.be/|/video/|vimeo\.com/\d+|tiktok\.com|instagram\.com/p/|x\.com/[^/]+/status|twitter\.com/[^/]+/status|pornhub\.com/view_video\.php|reddit\.com/r/[^/]+/comments/)", RegexOptions.IgnoreCase)]
    private static partial Regex VideoRegex();
}
