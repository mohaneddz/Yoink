using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Yoink.Application.Abstractions;
using Yoink.Domain.Media;
using Yoink.Infrastructure;
using Yoink.Shared;

namespace Yoink.Tests;

public class UniversalDownloadTests
{
    private static IMediaInspector CreateInspector()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddYoinkInfrastructure();
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMediaInspector>();
    }

    [Theory]
    [InlineData("https://www.tiktok.com/@user/video/7123456789012345678", MediaSourceKind.Short)]
    [InlineData("https://vm.tiktok.com/ZM8abc123/", MediaSourceKind.Short)]
    [InlineData("https://www.instagram.com/reel/C8xyz123/", MediaSourceKind.Short)]
    [InlineData("https://www.instagram.com/reels/C8xyz123/", MediaSourceKind.Short)]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", MediaSourceKind.Short)]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", MediaSourceKind.Video)]
    [InlineData("https://vimeo.com/123456789", MediaSourceKind.Video)]
    [InlineData("https://x.com/creator/status/1234567890", MediaSourceKind.Video)]
    [InlineData("https://twitter.com/creator/status/1234567890", MediaSourceKind.Video)]
    [InlineData("https://www.pornhub.com/view_video.php?viewkey=649b819f20627", MediaSourceKind.Video)]
    [InlineData("https://reddit.com/r/videos/comments/12345/cool_clip/", MediaSourceKind.Video)]
    [InlineData("https://cdn.example.com/live/stream.m3u8", MediaSourceKind.Video)]
    [InlineData("https://www.youtube.com/playlist?list=PL123456789", MediaSourceKind.Playlist)]
    public void DetectKind_classifies_platforms_accurately(string url, MediaSourceKind expectedKind)
    {
        var inspector = CreateInspector();
        var detected = inspector.DetectKind(url);
        Assert.Equal(expectedKind, detected);
    }
}
