using System.Net;
using Yoink.Infrastructure.Media;

namespace Yoink.Tests;

public class ImageFileCacheTests
{
    [Fact]
    public async Task Concurrent_requests_share_one_fetch_and_a_new_cache_instance_reuses_disk()
    {
        using var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async _ =>
        {
            await release.Task;
            return Response(new byte[] { 1, 2, 3 });
        });
        using var client = new HttpClient(handler);
        var cache = new ImageFileCache(client, fixture.Directory);
        var requests = Enumerable.Range(0, 20).Select(_ => cache.GetAsync("https://example.com/image.jpg")).ToArray();
        Assert.Equal(1, handler.Calls);
        release.SetResult();
        var paths = await Task.WhenAll(requests);
        Assert.All(paths, path => Assert.Equal(paths[0], path));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(paths[0]!));
        var reopened = new ImageFileCache(client, fixture.Directory);
        Assert.Equal(paths[0], await reopened.GetAsync("https://example.com/image.jpg"));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Expired_images_refresh_but_remain_available_offline()
    {
        using var fixture = new Fixture();
        using var handler = new Handler(_ => Task.FromResult(Response(new byte[] { 1 })));
        using var client = new HttpClient(handler);
        var cache = new ImageFileCache(client, fixture.Directory);
        var path = await cache.GetAsync("https://example.com/image.jpg");
        File.SetLastWriteTimeUtc(path!, DateTime.UtcNow.AddDays(-31));
        handler.Respond = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Equal(path, await cache.GetAsync("https://example.com/image.jpg"));
        Assert.Equal(2, handler.Calls);
        handler.Respond = _ => Task.FromResult(Response(new byte[] { 2 }));
        Assert.Equal(path, await cache.GetAsync("https://example.com/image.jpg"));
        Assert.Equal(new byte[] { 2 }, await File.ReadAllBytesAsync(path!));
    }

    [Fact]
    public async Task Failed_or_oversized_downloads_are_not_cached_and_can_be_retried()
    {
        using var fixture = new Fixture();
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var client = new HttpClient(handler);
        var cache = new ImageFileCache(client, fixture.Directory);
        Assert.Null(await cache.GetAsync("https://example.com/image.jpg"));
        handler.Respond = _ => Task.FromResult(Response(new byte[8 * 1024 * 1024 + 1]));
        Assert.Null(await cache.GetAsync("https://example.com/image.jpg"));
        handler.Respond = _ => Task.FromResult(Response(new byte[] { 3 }));
        Assert.NotNull(await cache.GetAsync("https://example.com/image.jpg"));
        Assert.Equal(3, handler.Calls);
        Assert.Single(System.IO.Directory.GetFiles(fixture.Directory));
    }

    [Fact]
    public async Task Slow_sources_use_at_most_six_simultaneous_connections()
    {
        using var fixture = new Fixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sixStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async _ =>
        {
            if (Interlocked.Increment(ref fixture.Active) == 6) sixStarted.TrySetResult();
            await release.Task;
            Interlocked.Decrement(ref fixture.Active);
            return Response(new byte[] { 1 });
        });
        using var client = new HttpClient(handler);
        var cache = new ImageFileCache(client, fixture.Directory);
        var requests = Enumerable.Range(0, 12).Select(i => cache.GetAsync($"https://example.com/{i}.jpg")).ToArray();
        try
        {
            await sixStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(6, handler.Calls);
        }
        finally { release.TrySetResult(); }
        Assert.All(await Task.WhenAll(requests), Assert.NotNull);
    }

    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private int _calls;
        public int Calls => _calls;
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Respond { get; set; } = send;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _calls);
            return Respond(request);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "yoink-image-test-" + Guid.NewGuid().ToString("N"));
        public int Active;
        public void Dispose()
        {
            if (!System.IO.Directory.Exists(Directory)) return;
            foreach (var file in System.IO.Directory.GetFiles(Directory)) File.Delete(file);
            System.IO.Directory.Delete(Directory);
        }
    }
}
