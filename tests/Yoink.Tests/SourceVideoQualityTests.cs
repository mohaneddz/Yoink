using Yoink.Domain.Settings;

namespace Yoink.Tests;

public class SourceVideoQualityTests
{
    [Theory]
    [InlineData("https://youtu.be/abc", "youtube.com")]
    [InlineData("https://m.youtube.com/watch?v=abc", "youtube.com")]
    [InlineData("https://www.tiktok.com/@user/video/1", "tiktok.com")]
    [InlineData("https://www.example.com/video", "example.com")]
    [InlineData("not a url", null)]
    public void SourceKeysGroupWebsiteVariants(string url, string? expected) =>
        Assert.Equal(expected, SourceVideoQuality.SourceKey(url));

    [Fact]
    public void ClosestHeightPrefersLowerOnTieAndHandlesEmpty() 
    {
        Assert.Equal(480, SourceVideoQuality.ClosestHeight(new[] { 1080, 720, 480, 360 }, 500));
        Assert.Equal(360, SourceVideoQuality.ClosestHeight(new[] { 480, 360 }, 420));
        Assert.Null(SourceVideoQuality.ClosestHeight(Array.Empty<int>(), 480));
    }
}
