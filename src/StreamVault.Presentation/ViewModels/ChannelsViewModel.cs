using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Application.Downloads;
using StreamVault.Domain.Entities;
using StreamVault.Domain.Media;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels.Models;
using StreamVault.Shared;

namespace StreamVault.Presentation.ViewModels;

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
        _ = LoadAsync();
    }

    [ObservableProperty] private string _addChannelUrl = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<SavedChannel> Channels { get; } = new();
    public ObservableCollection<ChannelUpload> Uploads { get; } = new();

    [ObservableProperty] private SavedChannel? _selectedChannel;
    [ObservableProperty] private string _selectedTab = "Recent Uploads";

    public int SelectedUploadCount => Uploads.Count(u => u.IsSelected);

    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

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
            var result = await _inspector.InspectPlaylistAsync(channel.ChannelUrl, limit: 50);
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

    private static SavedChannel Map(SavedChannelEntry e) => new()
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
        LastCheckedText = Formatting.ShortDate(e.LastCheckedAt),
        NextCheckText = "—"
    };
}
