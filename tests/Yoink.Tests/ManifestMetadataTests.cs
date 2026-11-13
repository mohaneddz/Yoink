using System.Text.Json;
using Yoink.Domain.Media;
using Yoink.Infrastructure.Ytdlp;

namespace Yoink.Tests;

public class ManifestMetadataTests
{
    [Fact]
    public void Decimal_manifest_dimensions_and_sizes_are_read_without_losing_resolutions()
    {
        using var json = JsonDocument.Parse("""{"height":480.0,"filesize_approx":18000000.5,"oversized":9999999999999}""");
        Assert.Equal(480, json.RootElement.GetInt("height"));
        Assert.Equal(18_000_000L, json.RootElement.GetLong("filesize_approx"));
        Assert.Null(json.RootElement.GetInt("oversized"));
    }

    [Fact]
    public void Video_dimensions_identify_streams_even_without_explicit_codec_metadata()
    {
        Assert.True(new MediaFormat { FormatId = "manifest", Height = 480 }.HasVideo);
        Assert.False(new MediaFormat { FormatId = "audio", Height = 480, VideoCodec = "none" }.HasVideo);
        Assert.False(new MediaFormat { FormatId = "audio", AudioCodec = "aac" }.HasVideo);
    }
}
