using Yoink.Domain.Media;
using Yoink.Presentation.Services;

namespace Yoink.Tests;

public class ChannelUploadTrackerTests
{
    [Fact]
    public void First_check_sets_baseline_without_announcing_old_uploads()
    {
        var result = ChannelUploadTracker.Compare("[]", [Entry("old"), Entry("older")]);

        Assert.Equal(0, result.NewCount);
        Assert.Contains("old", result.Snapshot);
    }

    [Fact]
    public void Later_check_counts_only_unseen_ids()
    {
        var baseline = ChannelUploadTracker.Compare(null, [Entry("old"), Entry("older")]);
        var result = ChannelUploadTracker.Compare(baseline.Snapshot, [Entry("new"), Entry("old"), Entry("older")]);

        Assert.Equal(1, result.NewCount);
        Assert.Equal(0, ChannelUploadTracker.Compare(result.Snapshot, [Entry("new"), Entry("old")]).NewCount);
    }

    [Fact]
    public void Corrupt_snapshot_is_replaced_without_false_alert()
    {
        var result = ChannelUploadTracker.Compare("not json", [Entry("existing")]);

        Assert.Equal(0, result.NewCount);
        Assert.Equal("[\"existing\"]", result.Snapshot);
    }

    private static PlaylistEntry Entry(string id) => new() { Id = id, Url = $"https://example.test/{id}", Title = id };
}
