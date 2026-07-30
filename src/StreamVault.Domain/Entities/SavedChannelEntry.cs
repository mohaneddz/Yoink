namespace StreamVault.Domain.Entities;

/// <summary>A channel the user follows for review/queueing (backs the Channels page).</summary>
public sealed class SavedChannelEntry
{
    public int Id { get; set; }
    public string ChannelUrl { get; set; } = string.Empty;
    public string ChannelId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public long SubscriberCount { get; set; }
    public long VideoCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AvatarUrl { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastCheckedAt { get; set; }
}
