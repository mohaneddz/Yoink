using Microsoft.Extensions.Logging.Abstractions;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Media;
using Yoink.Domain.Settings;
using Yoink.Infrastructure.Processes;
using Yoink.Infrastructure.Ytdlp;
using Yoink.Shared;

namespace Yoink.Tests;

public class DownloadPerformanceTests
{
    [Theory]
    [InlineData(8, null, 8)]
    [InlineData(16, "socks5://localhost:9150", 4)]
    [InlineData(1, "socks5://localhost:9150", 1)]
    [InlineData(999, null, 16)]
    [InlineData(0, null, 1)]
    public void Fragment_parallelism_respects_requested_limit_and_proxy_capacity(int requested, string? proxy, int expected) =>
        Assert.Equal(expected, YtDlpDownloadEngine.EffectiveFragmentCount(requested, proxy));

    [Fact]
    public void Prepared_download_skips_extraction_and_retries_stale_links_quickly()
    {
        var engine = new YtDlpDownloadEngine(new ProcessRunner(), new TestTools(), new DirectProxy(),
            new TestSettings(), new PreparedDownloadCache(), NullLogger<YtDlpDownloadEngine>.Instance);
        var request = new DownloadRequest { Url = "https://example.com/video", OutputFolder = "downloads" };
        var prepared = engine.BuildArguments(request, "result.path", null, "prepared.info.json");
        Assert.Contains("--load-info-json", prepared);
        Assert.Contains("prepared.info.json", prepared);
        Assert.DoesNotContain(request.Url, prepared);
        Assert.Equal("1", prepared[prepared.IndexOf("--retries") + 1]);
        Assert.Contains("--continue", prepared);
        Assert.DoesNotContain("--user-agent", prepared);
        var fresh = engine.BuildArguments(request, "result.path", null);
        Assert.DoesNotContain("--load-info-json", fresh);
        Assert.Contains(request.Url, fresh);
        Assert.Equal("10", fresh[fresh.IndexOf("--retries") + 1]);
    }

    [Fact]
    public void Prepared_links_are_isolated_by_connection_settings_and_expire()
    {
        var clock = new TestClock();
        var cache = new PreparedDownloadCache(clock);
        var media = new MediaMetadata
        {
            Id = "test", Title = "Synthetic media", WebpageUrl = "https://example.com/video",
            Formats = new[] { new MediaFormat { FormatId = "hls", Height = 480 } }
        };
        cache.Store(media.WebpageUrl, "proxy-a", "cookies-a", "prepared-json", media);
        Assert.Equal("prepared-json", cache.Get(media.WebpageUrl, "proxy-a", "cookies-a"));
        Assert.Null(cache.Get(media.WebpageUrl, "proxy-b", "cookies-a"));
        Assert.Null(cache.Get(media.WebpageUrl, "proxy-a", "cookies-b"));
        clock.Now = clock.Now.AddMinutes(3);
        Assert.Null(cache.Get(media.WebpageUrl, "proxy-a", "cookies-a"));
    }

    private sealed class DirectProxy : IProxyResolver
    {
        public Task<string?> ResolveAsync(string url, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

internal sealed class TestSettings : ISettingsService
{
    public AppSettings Current { get; private set; } = new();
    public int Saves { get; private set; }
    public event EventHandler? Changed;
    public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Current = settings;
        Saves++;
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }
}

internal class TestTools : IToolProvisioner
{
    public ToolPaths Paths { get; init; } = new("yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe");
    public bool IsReady => true;
    public ToolUpdateInfo LastUpdateInfo { get; set; } = new(false, "test", "test", false, "test", false, DateTimeOffset.UtcNow);
    public Task<Result<ToolPaths>> EnsureAsync(IProgress<string>? log = null, CancellationToken ct = default) => Task.FromResult(Result<ToolPaths>.Success(Paths));
    public virtual Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ToolStatus>>(Array.Empty<ToolStatus>());
    public virtual Task<ToolUpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default) => Task.FromResult(LastUpdateInfo);
    public virtual Task<Result> UpdateAllToolsAsync(IProgress<string>? log = null, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> UpdateYtDlpAsync(IProgress<string>? log = null, CancellationToken ct = default) => Task.FromResult(Result.Success());
    public Task<Result> UpdateFfmpegAsync(IProgress<string>? log = null, CancellationToken ct = default) => Task.FromResult(Result.Success());
}
