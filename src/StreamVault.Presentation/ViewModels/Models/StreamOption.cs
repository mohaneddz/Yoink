namespace StreamVault.Presentation.ViewModels.Models;

/// <summary>Row in the "available streams" table (spec section 7.1).</summary>
public sealed class StreamOption
{
    public required string Resolution { get; init; }
    public required string Format { get; init; }
    public required string VideoCodec { get; init; }
    public required string AudioCodec { get; init; }
    public int Fps { get; init; }
    public bool Hdr { get; init; }
    public required string Size { get; init; }
    public bool IsRecommended { get; init; }
}
