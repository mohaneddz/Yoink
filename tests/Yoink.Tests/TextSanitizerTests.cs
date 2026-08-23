using Yoink.Shared.Validation;

namespace Yoink.Tests;

public class TextSanitizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("https://www.youtube.com/watch?v=8mLm8RdpzvI", "https://www.youtube.com/watch?v=8mLm8RdpzvI")]
    [InlineData("  https://www.youtube.com/watch?v=8mLm8RdpzvI  ", "https://www.youtube.com/watch?v=8mLm8RdpzvI")]
    public void TruncateToSingleLine_handles_single_line_and_empty_inputs(string? input, string expected)
    {
        Assert.Equal(expected, TextSanitizer.TruncateToSingleLine(input));
    }

    [Fact]
    public void TruncateToSingleLine_truncates_multiline_paste_to_first_line()
    {
        var multiline = "09:42:12  Queued: NumPy Explained in 2 Minutes\r\n09:42:12  Starting download\r\n09:42:15  ERROR: unable to download video data";
        var result = TextSanitizer.TruncateToSingleLine(multiline);

        Assert.Equal("09:42:12  Queued: NumPy Explained in 2 Minutes", result);
    }

    [Fact]
    public void TruncateToSingleLine_skips_leading_empty_lines_and_returns_first_non_empty()
    {
        var multiline = "\r\n  \n\r\nhttps://www.youtube.com/playlist?list=PL123\r\nsecond line";
        var result = TextSanitizer.TruncateToSingleLine(multiline);

        Assert.Equal("https://www.youtube.com/playlist?list=PL123", result);
    }

    [Theory]
    [InlineData("Line1\rLine2", "Line1")]
    [InlineData("Line1\nLine2", "Line1")]
    [InlineData("Line1\r\nLine2", "Line1")]
    public void TruncateToSingleLine_supports_all_newline_styles(string input, string expected)
    {
        Assert.Equal(expected, TextSanitizer.TruncateToSingleLine(input));
    }
}
