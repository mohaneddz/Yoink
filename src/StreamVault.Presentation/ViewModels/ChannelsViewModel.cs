using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Presentation.ViewModels.Models;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the Channels page (spec section 7.4): save channels, review uploads, queue content.</summary>
public partial class ChannelsViewModel : ObservableObject
{
    public ChannelsViewModel()
    {
        LoadSampleData();
    }

    [ObservableProperty] private string _addChannelUrl = string.Empty;

    public ObservableCollection<SavedChannel> Channels { get; } = new();
    public ObservableCollection<ChannelUpload> Uploads { get; } = new();

    [ObservableProperty]
    private SavedChannel? _selectedChannel;

    [ObservableProperty]
    private string _selectedTab = "Recent Uploads";

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    [RelayCommand]
    private void SelectChannel(SavedChannel channel) => SelectedChannel = channel;

    public int SelectedUploadCount => Uploads.Count(u => u.IsSelected);

    [RelayCommand] private void AddChannel() { }
    [RelayCommand] private void AddSelectedToQueue() { }
    [RelayCommand] private void DownloadLatest() { }
    [RelayCommand] private void MarkAllAsSeen() { }
    [RelayCommand] private void UnfollowChannel() { }
    [RelayCommand] private void OpenChannel() { }

    private void LoadSampleData()
    {
        var scenic = new SavedChannel
        {
            Name = "Scenic Relaxation", Handle = "@ScenicRelaxation", Initial = "S", AvatarHex = "#FFFF4F0A",
            SubscriberCountText = "2.41M", VideoCountText = "812", ShortCountText = "167", LiveCountText = "12", PlaylistCountText = "28",
            LastCheckedText = "May 22, 2025 13:41", NextCheckText = "May 22, 2025 19:41", TotalViewsText = "3.2B",
            JoinedText = "May 4, 2012", CountryText = "Switzerland", LanguageText = "English",
            Description = "Experience the breathtaking beauty of our planet with stunning 4K films set to relaxing music. New videos every week!"
        };
        Channels.Add(scenic);
        Channels.Add(new SavedChannel { Name = "Linus Tech Tips", Handle = "@LinusTechTips", Initial = "L", AvatarHex = "#FFD93025", SubscriberCountText = "15.6M", VideoCountText = "7.1K", ShortCountText = "912", LiveCountText = "34", PlaylistCountText = "156", LastCheckedText = "May 22, 2025 13:20" });
        Channels.Add(new SavedChannel { Name = "Veritasium", Handle = "@veritasium", Initial = "V", AvatarHex = "#FF1F6FEB", SubscriberCountText = "17.1M", VideoCountText = "7.1K", ShortCountText = "241", LiveCountText = "0", PlaylistCountText = "54", LastCheckedText = "May 22, 2025 12:55" });
        Channels.Add(new SavedChannel { Name = "Kurzgesagt", Handle = "@kurzgesagt", Initial = "K", AvatarHex = "#FF21A33A", SubscriberCountText = "23.2M", VideoCountText = "241", ShortCountText = "98", LiveCountText = "1", PlaylistCountText = "22", LastCheckedText = "May 22, 2025 11:30" });
        Channels.Add(new SavedChannel { Name = "MrBeast", Handle = "@MrBeast", Initial = "M", AvatarHex = "#FF1FA2FF", SubscriberCountText = "321M", VideoCountText = "854", ShortCountText = "612", LiveCountText = "11", PlaylistCountText = "43", LastCheckedText = "May 22, 2025 10:05" });

        SelectedChannel = scenic;

        Uploads.Add(new ChannelUpload { Title = "Switzerland 4K - Scenic Relaxation Film with Calming Music", DateText = "May 20, 2025", ViewsText = "26.4M views", Type = ChannelUploadType.Video, DurationText = "12:47" });
        Uploads.Add(new ChannelUpload { Title = "Nature's Beauty in 60 Seconds", DateText = "May 19, 2025", ViewsText = "3.2M views", Type = ChannelUploadType.Short, DurationText = "0:58" });
        Uploads.Add(new ChannelUpload { Title = "Live: Relaxing Mountain Views 24/7", DateText = "May 18, 2025", ViewsText = "18.9K watching", Type = ChannelUploadType.Live });
        Uploads.Add(new ChannelUpload { Title = "Ocean Waves for Deep Sleep 8 Hours", DateText = "May 17, 2025", ViewsText = "9.1M views", Type = ChannelUploadType.Video, DurationText = "10:21" });
        Uploads.Add(new ChannelUpload { Title = "Forest Sounds & Birds Chirping Ambience", DateText = "May 16, 2025", ViewsText = "4.7M views", Type = ChannelUploadType.Playlist, DurationText = "1:05:32" });
    }
}
