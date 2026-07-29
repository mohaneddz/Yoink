namespace StreamVault.Presentation.ViewModels;

/// <summary>Stand-in page content used until each real page view model lands (milestone M2).</summary>
public sealed class PlaceholderPageViewModel
{
    public required string Title { get; init; }
    public required string Tagline { get; init; }
}
