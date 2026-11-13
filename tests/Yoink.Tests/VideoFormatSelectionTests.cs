using System.Reflection;
using Yoink.Application.Downloads;
using Yoink.Infrastructure.Ytdlp;

namespace Yoink.Tests;

public class VideoFormatSelectionTests
{
    private static string Selector(DownloadRequest request) => (string)typeof(YtDlpDownloadEngine)
        .GetMethod("BuildVideoSelector", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, new object[] { request })!;

    [Theory]
    [InlineData("mp4", "hls-old+ba[ext=m4a]/hls-old+ba/hls-old/")]
    [InlineData("webm", "hls-old+ba[ext=webm]/hls-old+ba/hls-old/")]
    [InlineData("mkv", "hls-old+ba/hls-old/")]
    public void Explicit_stream_keeps_priority_and_falls_back_to_requested_quality(string container, string prefix)
    {
        var automatic = Selector(new DownloadRequest
        {
            Url = "https://example.com/video", MaxHeight = 480,
            VideoContainer = container, VideoCodecPreference = "H.264 (AVC)"
        });
        var explicitStream = Selector(new DownloadRequest
        {
            Url = "https://example.com/video", FormatId = "hls-old", MaxHeight = 480,
            VideoContainer = container, VideoCodecPreference = "H.264 (AVC)"
        });

        Assert.Equal(prefix + automatic, explicitStream);
        Assert.Contains("[height<=?480][vcodec^=avc1]", automatic);
    }
}
