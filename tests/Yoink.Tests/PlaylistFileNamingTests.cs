using Yoink.Presentation.Services;

namespace Yoink.Tests;

public class PlaylistFileNamingTests
{
    [Theory]
    [InlineData("Sequential (01, 02, 03)", 3, "03 - %(title)s.%(ext)s")]
    [InlineData("Plain (1, 2, 3)", 3, "3 - %(title)s.%(ext)s")]
    [InlineData("None", 3, "%(title)s.%(ext)s")]
    public void Numbering_resolves_for_individual_video_downloads(string pattern, int index, string expected)
    {
        Assert.Equal(expected, PlaylistFileNaming.Resolve("%(playlist_index)02d - %(title)s.%(ext)s", index, pattern));
    }

    [Fact]
    public void Custom_template_is_preserved()
    {
        Assert.Equal("%(uploader)s - %(title)s.%(ext)s",
            PlaylistFileNaming.Resolve("%(uploader)s - %(title)s.%(ext)s", 12, "Plain (1, 2, 3)"));
    }
}
