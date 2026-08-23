using Yoink.Shared.Validation;

namespace Yoink.Tests;

public class ChannelWorkflowTests
{
    [Theory]
    [InlineData("@ShadeOfCode", "https://www.youtube.com/@ShadeOfCode")]
    [InlineData("  @Cherry  ", "https://www.youtube.com/@Cherry")]
    [InlineData("youtube.com/@Veritasium", "https://youtube.com/@Veritasium")]
    [InlineData("www.youtube.com/@Veritasium", "https://www.youtube.com/@Veritasium")]
    [InlineData("https://www.youtube.com/@3blue1brown", "https://www.youtube.com/@3blue1brown")]
    [InlineData("3blue1brown", "https://www.youtube.com/@3blue1brown")]
    public void NormalizeChannelInput_normalizes_various_formats(string input, string expected)
    {
        var result = TextSanitizer.NormalizeChannelInput(input);
        Assert.Equal(expected, result);
    }
}
