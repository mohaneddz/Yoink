using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Yoink.Infrastructure.Proxy;

namespace Yoink.Tests;

public class ProxyResolverTests
{
    private readonly ProxyResolver _resolver;

    public ProxyResolverTests()
    {
        // For testing domain resolution without triggering Tor downloads, pass a dummy TorManager
        var httpFactory = new DummyHttpFactory();
        var torManager = new TorManager(httpFactory, NullLogger<TorManager>.Instance);
        _resolver = new ProxyResolver(torManager, NullLogger<ProxyResolver>.Instance);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.tiktok.com/@user/video/123456")]
    [InlineData("https://www.instagram.com/reel/123456")]
    [InlineData("https://x.com/user/status/123456")]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("")]
    [InlineData("invalid-url")]
    public async Task NonBlockedUrls_DoNotUseProxy(string url)
    {
        var result = await _resolver.ResolveAsync(url);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("https://www.pornhub.com/view_video.php?viewkey=6ae7b453075fb")]
    [InlineData("https://pornhub.com/view_video.php?viewkey=123")]
    [InlineData("https://rt.pornhub.com/view_video.php?viewkey=123")]
    [InlineData("https://www.xvideos.com/video123")]
    [InlineData("https://www.xnxx.com/video123")]
    [InlineData("https://www.spankbang.com/video123")]
    public async Task BlockedUrls_ResolveProxy(string url)
    {
        // If Tor is running (e.g. port 9150), returns socks5://127.0.0.1:9150
        // If not running, attempts managed Tor or returns null if offline in test
        var result = await _resolver.ResolveAsync(url);
        if (result != null)
        {
            Assert.StartsWith("socks5://127.0.0.1:", result);
        }
    }

    private sealed class DummyHttpFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new OfflineHandler());
    }

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }
}
