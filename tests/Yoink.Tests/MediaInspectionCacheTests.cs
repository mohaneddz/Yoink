using Yoink.Domain.Media;
using Yoink.Infrastructure.Ytdlp;
using Yoink.Shared;

namespace Yoink.Tests;

public class MediaInspectionCacheTests
{
    private static readonly (string, string, string) Key = ("https://example.com/video", "", "");
    private static MediaMetadata Media(bool live = false, bool formats = true) => new()
    {
        Id = "test", Title = "Synthetic media", WebpageUrl = Key.Item1, IsLive = live,
        Formats = formats ? new[] { new MediaFormat { FormatId = "hls", VideoCodec = "avc1" } } : Array.Empty<MediaFormat>()
    };

    [Fact]
    public async Task Concurrent_and_repeated_requests_share_successful_inspection()
    {
        var cache = new MediaInspectionCache();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async Task<Result<MediaMetadata>> Inspect(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            await release.Task.WaitAsync(ct);
            return Result<MediaMetadata>.Success(Media());
        }
        var first = cache.GetAsync(Key, Inspect, false, default);
        var second = cache.GetAsync(Key, Inspect, false, default);
        Assert.Equal(1, calls);
        release.SetResult();
        var results = await Task.WhenAll(first, second);
        Assert.Same(results[0].Value, results[1].Value);
        await cache.GetAsync(Key, Inspect, false, default);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Forced_refresh_and_connection_settings_bypass_cached_metadata()
    {
        var cache = new MediaInspectionCache();
        var calls = 0;
        Task<Result<MediaMetadata>> Inspect(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(Result<MediaMetadata>.Success(Media()));
        }
        await cache.GetAsync(Key, Inspect, false, default);
        await cache.GetAsync(Key, Inspect, true, default);
        await cache.GetAsync((Key.Item1, "socks5://localhost:1234", ""), Inspect, false, default);
        await cache.GetAsync((Key.Item1, "", "cookies.txt"), Inspect, false, default);
        Assert.Equal(4, calls);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Live_or_empty_results_are_not_cached(bool live, bool formats)
    {
        var cache = new MediaInspectionCache();
        var calls = 0;
        Task<Result<MediaMetadata>> Inspect(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(Result<MediaMetadata>.Success(Media(live, formats)));
        }
        await cache.GetAsync(Key, Inspect, false, default);
        await cache.GetAsync(Key, Inspect, false, default);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Cancellation_releases_inspection_and_failure_is_not_cached()
    {
        var cache = new MediaInspectionCache();
        using var cancellation = new CancellationTokenSource();
        var pending = cache.GetAsync(Key, async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Result<MediaMetadata>.Success(Media());
        }, false, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var failure = await cache.GetAsync(Key, _ => Task.FromResult(Result<MediaMetadata>.Failure("Transient error")), false, default);
        Assert.True(failure.IsFailure);
        var success = await cache.GetAsync(Key, _ => Task.FromResult(Result<MediaMetadata>.Success(Media())), false, default);
        Assert.True(success.IsSuccess);
    }

    [Fact]
    public async Task Expired_metadata_is_refetched()
    {
        var clock = new TestClock();
        var cache = new MediaInspectionCache(clock);
        var calls = 0;
        Task<Result<MediaMetadata>> Inspect(CancellationToken ct)
        {
            calls++;
            return Task.FromResult(Result<MediaMetadata>.Success(Media()));
        }
        await cache.GetAsync(Key, Inspect, false, default);
        clock.Now = clock.Now.AddMinutes(3);
        await cache.GetAsync(Key, Inspect, false, default);
        Assert.Equal(2, calls);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
