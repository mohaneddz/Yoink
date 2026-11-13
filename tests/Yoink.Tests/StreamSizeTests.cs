using Yoink.Domain.Media;

namespace Yoink.Tests;

public class StreamSizeTests
{
    [Fact]
    public void Exact_size_takes_priority_over_approximations()
    {
        var format = new MediaFormat { FormatId = "video", FilesizeBytes = 100, FilesizeApproxBytes = 200, TotalBitrateKbps = 2000 };
        Assert.Equal(100L, format.EstimateSizeBytes(60));
    }

    [Fact]
    public void Manifest_bitrate_and_duration_produce_an_estimate()
    {
        var format = new MediaFormat { FormatId = "hls", TotalBitrateKbps = 2400 };
        Assert.Equal(18_000_000L, format.EstimateSizeBytes(60));
        Assert.Null(format.EstimateSizeBytes(null));
        Assert.Null(format.EstimateSizeBytes(double.PositiveInfinity));
    }

    [Fact]
    public void Component_bitrates_are_used_when_total_is_missing()
    {
        var format = new MediaFormat { FormatId = "combined", VideoBitrateKbps = 2000, AudioBitrateKbps = 128 };
        Assert.Equal(15_960_000L, format.EstimateSizeBytes(60));
    }

    [Fact]
    public void Invalid_sizes_do_not_hide_a_valid_approximation()
    {
        var format = new MediaFormat { FormatId = "video", FilesizeBytes = 0, FilesizeApproxBytes = 200 };
        Assert.Equal(200L, format.EstimateSizeBytes(null));
    }
}
