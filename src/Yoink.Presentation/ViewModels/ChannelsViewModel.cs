using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Entities;
using Yoink.Domain.Media;
using Yoink.Presentation.Navigation;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Presentation.ViewModels;

/// <summary>Backs the Channels page: follow channels, review uploads, and queue their content.</summary>
public partial class ChannelsViewModel : ObservableObject
{
    private readonly ISavedChannelRepository _repository;
    private readonly IMediaInspector _inspector;
    private readonly DownloadCoordinator _coordinator;
    private readonly ISettingsService _settings;
    private readonly ISystemAccess _system;
    private readonly INavigationService _navigation;

    public ChannelsViewModel(
        ISavedChannelRepository repository,
        IMediaInspector inspector,
        DownloadCoordinator coordinator,
        ISettingsService settings,
        ISystemAccess system,
        INavigationService navigation)
    {
        _repository = repository;
        _inspector = inspector;
        _coordinator = coordinator;
        _settings = settings;
        _system = system;
        _navigation = navigation;
        _uploadsView = CollectionViewSource.GetDefaultView(Uploads);
        _uploadsView.Filter = FilterUpload;
        _ = LoadAsync();
    }

    [ObservableProperty] private string _addChannelUrl = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    private readonly ICollectionView _uploadsView;
    private int _uploadLimit = 50;

    public ObservableCollection<SavedChannel> Channels { get; } = new();
    public ObservableCollection<ChannelUpload> Uploads { get; } = new();
    public ICollectionView UploadsView => _uploadsView;

    [ObservableProperty] private SavedChannel? _selectedChannel;
    [ObservableProperty] private string _selectedTab = "Recent Uploads";

    public int SelectedUploadCount => Uploads.Count(u => u.IsSelected);

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    partial void OnSelectedTabChanged(string value) => _uploadsView.Refresh();

    private bool FilterUpload(object obj)
    {
        if (obj is not ChannelUpload upload)
        {
            return false;
        }

        return SelectedTab?.ToLowerInvariant() switch
        {
            "videos" => upload.Type == ChannelUploadType.Video,
            "shorts" => upload.Type == ChannelUploadType.Short,
            "livestreams" => upload.Type == ChannelUploadType.Live,
            "playlists" => upload.Type == ChannelUploadType.Playlist,
            _ => true
        };
    }

    [RelayCommand]
    private async Task LoadMore()
    {
        if (SelectedChannel is not null)
        {
            _uploadLimit += 50;
            await LoadUploadsAsync(SelectedChannel);
        }
    }

    [RelayCommand]
    private async Task SelectChannel(SavedChannel channel)
    {
        SelectedChannel = channel;
        await LoadUploadsAsync(channel);
    }

    partial void OnSelectedChannelChanged(SavedChannel? value)
    {
        foreach (var c in Channels)
        {
            c.IsSelected = c == value;
        }
    }

    public async Task LoadAsync()
    {
        var entries = await _repository.GetAllAsync();
        Channels.Clear();
        foreach (var e in entries)
        {
            Channels.Add(Map(e));
        }

        if (Channels.Count > 0)
        {
            await SelectChannel(Channels[0]);
        }
    }

    [RelayCommand]
    private async Task AddChannel()
    {
        var url = AddChannelUrl.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusMessage = "Paste a channel URL first.";
            return;
        }

        if (Channels.Any(c => string.Equals(c.ChannelUrl, url, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Channel already saved.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Fetching channel…";
        try
        {
            var result = await _inspector.InspectPlaylistAsync(url, limit: 30);
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Could not fetch channel.";
                return;
            }

            var meta = result.Value!;
            var entry = new SavedChannelEntry
            {
                ChannelUrl = url,
                ChannelId = meta.ChannelId ?? string.Empty,
                Name = meta.Title,
                Handle = meta.Uploader ?? string.Empty,
                Verified = false,
                VideoCount = meta.Entries.Count,
                Description = meta.Description ?? string.Empty,
                AddedAt = DateTimeOffset.Now,
                LastCheckedAt = DateTimeOffset.Now
            };
            await _repository.AddAsync(entry);

            var channel = Map(entry);
            Channels.Add(channel);
            AddChannelUrl = string.Empty;
            StatusMessage = $"Added {channel.Name}.";
            await SelectChannel(channel);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadUploadsAsync(SavedChannel channel)
    {
        Uploads.Clear();
        if (string.IsNullOrWhiteSpace(channel.ChannelUrl))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Loading uploads…";
        try
        {
            var result = await _inspector.InspectPlaylistAsync(channel.ChannelUrl, limit: _uploadLimit);
            if (result.IsFailure)
            {
                StatusMessage = result.Error ?? "Could not load uploads.";
                return;
            }

            foreach (var e in result.Value!.Entries)
            {
                var upload = new ChannelUpload
                {
                    Title = e.Title,
                    DateText = Formatting.RelativeDate(e.UploadDate),
                    DurationText = Formatting.Duration(e.DurationSeconds),
                    ViewsText = e.ViewCount is { } v ? $"{Formatting.Count(v)} views" : string.Empty,
                    Type = e.Url.Contains("/shorts/", StringComparison.OrdinalIgnoreCase) ? ChannelUploadType.Short : ChannelUploadType.Video
                };
                upload.Url = e.Url;
                upload.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ChannelUpload.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedUploadCount));
                    }
                };
                Uploads.Add(upload);
            }

            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(SelectedUploadCount));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddSelectedToQueue()
    {
        var selected = Uploads.Where(u => u.IsSelected).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "Select uploads to queue.";
            return;
        }

        foreach (var upload in selected)
        {
            Enqueue(upload, start: false);
        }

        StatusMessage = $"{selected.Count} added to queue.";
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void DownloadLatest()
    {
        var latest = Uploads.FirstOrDefault(u => u.Type != ChannelUploadType.Live);
        if (latest is null)
        {
            StatusMessage = "No uploads to download.";
            return;
        }

        Enqueue(latest, start: true);
        _navigation.Navigate(AppPage.Queue);
    }

    private void Enqueue(ChannelUpload upload, bool start)
    {
        if (string.IsNullOrWhiteSpace(upload.Url))
        {
            return;
        }

        var settings = _settings.Current;
        var request = new DownloadRequest
        {
            Url = upload.Url,
            Kind = DownloadKind.Video,
            VideoContainer = settings.PreferredVideoContainer,
            AudioContainer = settings.PreferredAudioContainer,
            OutputFolder = settings.DefaultDownloadFolder,
            FilenameTemplate = settings.FilenameTemplate,
            EmbedMetadata = true,
            CookiesFile = settings.CookiesFile,
            Proxy = settings.Proxy,
            SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
            KnownTitle = upload.Title
        };

        _coordinator.Enqueue(request, new EnqueueInfo(upload.Title, SelectedChannel?.Name ?? string.Empty, "Best", settings.PreferredVideoContainer.ToUpperInvariant()), start);
        upload.Downloaded = true;
    }

    [RelayCommand]
    private void MarkAllAsSeen()
    {
        foreach (var upload in Uploads)
        {
            upload.Downloaded = true;
        }
    }

    [RelayCommand]
    private async Task UnfollowChannel()
    {
        if (SelectedChannel is null)
        {
            return;
        }

        if (SelectedChannel.EntryId > 0)
        {
            await _repository.DeleteAsync(SelectedChannel.EntryId);
        }

        Channels.Remove(SelectedChannel);
        Uploads.Clear();
        SelectedChannel = Channels.FirstOrDefault();
        if (SelectedChannel is not null)
        {
            await LoadUploadsAsync(SelectedChannel);
        }
    }

    [RelayCommand]
    private void OpenChannel()
    {
        if (!string.IsNullOrWhiteSpace(SelectedChannel?.ChannelUrl))
        {
            _system.OpenUrl(SelectedChannel.ChannelUrl);
        }
    }

    public ObservableCollection<int> CheckFrequencyOptions { get; } = new() { 1, 3, 6, 12, 24 };

    [RelayCommand]
    private async Task SetCheckFrequency(int hours)
    {
        if (SelectedChannel is null || hours <= 0)
        {
            return;
        }

        SelectedChannel.CheckFrequencyHours = hours;
        ApplyCheckFrequency(SelectedChannel);

        if (SelectedChannel.EntryId > 0)
        {
            var entries = await _repository.GetAllAsync();
            var entry = entries.FirstOrDefault(x => x.Id == SelectedChannel.EntryId);
            if (entry is not null)
            {
                entry.CheckFrequencyHours = hours;
                await _repository.UpdateAsync(entry);
            }
        }
    }

    private static void ApplyCheckFrequency(SavedChannel channel)
    {
        channel.CheckFrequencyText = FormatFrequency(channel.CheckFrequencyHours);
        channel.NextCheckText = ComputeNextCheck(channel);
    }

    internal static string FormatFrequency(int hours) => hours switch
    {
        1 => "1 hour",
        24 => "24 hours (daily)",
        _ => $"{hours} hours"
    };

    private static string ComputeNextCheck(SavedChannel channel) =>
        channel.LastCheckedAtRaw is { } last ? Formatting.ShortDate(last.AddHours(channel.CheckFrequencyHours)) : "Pending";

    private static SavedChannel Map(SavedChannelEntry e)
    {
        var channel = new SavedChannel
        {
            EntryId = e.Id,
            ChannelUrl = e.ChannelUrl,
            Name = e.Name,
            Handle = e.Handle,
            Initial = string.IsNullOrEmpty(e.Name) ? "?" : e.Name[..1].ToUpperInvariant(),
            Verified = e.Verified,
            SubscriberCountText = e.SubscriberCount > 0 ? Formatting.Count(e.SubscriberCount) : "—",
            VideoCountText = e.VideoCount > 0 ? e.VideoCount.ToString() : "—",
            ShortCountText = "—",
            LiveCountText = "—",
            PlaylistCountText = "—",
            TotalViewsText = "—",
            Description = e.Description,
            CountryText = string.IsNullOrWhiteSpace(e.Country) ? "—" : e.Country,
            LanguageText = "—",
            JoinedText = "—",
            LastCheckedAtRaw = e.LastCheckedAt,
            LastCheckedText = Formatting.ShortDate(e.LastCheckedAt),
            CheckFrequencyHours = e.CheckFrequencyHours <= 0 ? 6 : e.CheckFrequencyHours
        };
        ApplyCheckFrequency(channel);
        return channel;
    }
}
