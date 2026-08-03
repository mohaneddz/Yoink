using Yoink.Shared.Validation;

namespace Yoink.Tests;

public class FileNamingTests
{
    [Theory]
    [InlineData("Normal Title", "Normal Title")]
    [InlineData("Weird: Title / With * Bad? Chars\"<>|", "Weird_ Title _ With _ Bad_ Chars____")]
    [InlineData("Trailing dots and spaces...   ", "Trailing dots and spaces")]
    [InlineData("", "untitled")]
    [InlineData("   ", "untitled")]
    public void SanitizeSegment_handles_forbidden_characters_and_trimming(string input, string expected)
    {
        Assert.Equal(expected, FileNaming.SanitizeSegment(input));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public void SanitizeSegment_escapes_reserved_windows_device_names(string reserved)
    {
        var result = FileNaming.SanitizeSegment(reserved);
        Assert.NotEqual(reserved, result);
        Assert.EndsWith(reserved, result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SanitizeSegment_escapes_reserved_name_even_with_extension()
    {
        var result = FileNaming.SanitizeSegment("CON.mp4");
        Assert.Equal("_CON.mp4", result);
    }

    [Fact]
    public void SanitizeSegment_caps_length()
    {
        var longName = new string('a', 500);
        var result = FileNaming.SanitizeSegment(longName);
        Assert.True(result.Length <= FileNaming.MaxSegmentLength);
    }

    [Fact]
    public void SanitizeSegment_preserves_unicode()
    {
        Assert.Equal("日本語のタイトル", FileNaming.SanitizeSegment("日本語のタイトル"));
    }
}
