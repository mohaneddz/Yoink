using Yoink.Application.Abstractions;

namespace Yoink.Tests;

public class ToolUpdateTests
{
    [Fact]
    public void ToolUpdateInfo_record_constructs_and_reads_properties()
    {
        var now = DateTimeOffset.Now;
        var info = new ToolUpdateInfo(
            UpdateAvailable: true,
            CurrentYtDlpVersion: "2026.03.17",
            LatestYtDlpVersion: "2026.08.19",
            YtDlpUpdateAvailable: true,
            CurrentFfmpegVersion: "n7.0",
            FfmpegUpdateAvailable: false,
            LastChecked: now
        );

        Assert.True(info.UpdateAvailable);
        Assert.True(info.YtDlpUpdateAvailable);
        Assert.False(info.FfmpegUpdateAvailable);
        Assert.Equal("2026.03.17", info.CurrentYtDlpVersion);
        Assert.Equal("2026.08.19", info.LatestYtDlpVersion);
        Assert.Equal(now, info.LastChecked);
    }
}
