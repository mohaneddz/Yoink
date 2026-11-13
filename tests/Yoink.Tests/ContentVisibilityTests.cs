using System.Reflection;
using Yoink.Application.Abstractions;
using Yoink.Domain.Entities;
using Yoink.Domain.Media;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Tests;

public class ContentVisibilityTests
{
    private static IEnumerable<string> ConfiguredDomains =>
        (IEnumerable<string>)typeof(SourcePrivacy).GetField("PrivateDomains", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void Classification_matches_configured_hosts_and_subdomains_without_matching_lookalikes()
    {
        Assert.NotEmpty(ConfiguredDomains);
        foreach (var domain in ConfiguredDomains)
        {
            Assert.True(SourcePrivacy.IsPrivateSource($"https://{domain}/video"));
            Assert.True(SourcePrivacy.IsPrivateSource($"https://WWW.{domain.ToUpperInvariant()}/video"));
            Assert.False(SourcePrivacy.IsPrivateSource($"https://{domain}.example.com/video"));
            Assert.False(SourcePrivacy.IsPrivateSource($"https://example.com/?url=https://{domain}/video"));
            Assert.False(SourcePrivacy.IsPrivateSource($"https://{domain}@example.com/video"));
        }
        Assert.False(SourcePrivacy.IsPrivateSource("invalid-url"));
    }

    [Fact]
    public async Task Privacy_toggle_hides_and_restores_history_library_groups_and_queue_counts()
    {
        var settings = new TestSettings();
        var store = new Store($"https://{ConfiguredDomains.First()}/video");
        var inspector = new InspectorViewModel(settings);
        var library = new LibraryViewModel(store, store, null!, null!, settings);
        var history = new HistoryViewModel(inspector, store, null!, null!, null!, settings);
        var coordinator = new DownloadCoordinator(null!, null!, null!, null!, null!, settings, null!);
        coordinator.Jobs.Add(new QueueJob { SourceUrl = store.PrivateUrl, Status = QueueJobStatus.Completed, IsSelected = true });
        coordinator.Jobs.Add(new QueueJob { SourceUrl = "https://example.com/video", Status = QueueJobStatus.Completed });
        var queue = new QueueViewModel(inspector, coordinator, settings) { SelectedFilter = QueueFilter.Done };
        Assert.Equal(2, history.Records.Count);
        Assert.Equal(2, library.RecentDownloads.Count);
        history.SelectedRecord = history.Records.First(r => r.SourceUrl == store.PrivateUrl);
        Assert.True(inspector.IsOpen);

        settings.Current.HidePrivateVideos = true;
        await settings.SaveAsync(settings.Current);
        Assert.Single(history.Records);
        Assert.Single(library.RecentDownloads);
        Assert.Single(library.Channels);
        Assert.DoesNotContain(library.Channels, group => group.Name == "Private channel");
        Assert.Equal(1, queue.DoneCount);
        Assert.Single(queue.FilteredJobs.Cast<QueueJob>());
        Assert.False(coordinator.Jobs[0].IsSelected);
        Assert.Null(history.SelectedRecord);
        Assert.Null(inspector.Content);
        Assert.False(inspector.IsOpen);
        Assert.Equal(2, coordinator.Jobs.Count);

        settings.Current.HidePrivateVideos = false;
        await settings.SaveAsync(settings.Current);
        Assert.Equal(2, history.Records.Count);
        Assert.Equal(2, library.RecentDownloads.Count);
        Assert.Equal(2, queue.DoneCount);
        Assert.Equal(2, queue.FilteredJobs.Cast<QueueJob>().Count());
    }

    private sealed class Store(string privateUrl) : IHistoryRepository, ILibraryRepository, ISavedChannelRepository
    {
        public string PrivateUrl => privateUrl;
        Task<IReadOnlyList<DownloadHistoryEntry>> IHistoryRepository.GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DownloadHistoryEntry>>(new[]
            {
                new DownloadHistoryEntry { Id = 1, SourceUrl = privateUrl, Title = "Private item", Channel = "Private channel" },
                new DownloadHistoryEntry { Id = 2, SourceUrl = "https://example.com/video", Title = "Public item" }
            });
        Task<IReadOnlyList<LibraryEntry>> ILibraryRepository.GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LibraryEntry>>(new[]
            {
                new LibraryEntry { Id = 1, SourceUrl = privateUrl, Title = "Private item", Channel = "Private channel", Kind = DownloadKind.Video, FilePath = @"C:\private\video.mp4" },
                new LibraryEntry { Id = 2, SourceUrl = "https://example.com/video", Title = "Public item", Channel = "Public channel", Kind = DownloadKind.Video, FilePath = @"C:\public\video.mp4" }
            });
        Task<IReadOnlyList<SavedChannelEntry>> ISavedChannelRepository.GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SavedChannelEntry>>(Array.Empty<SavedChannelEntry>());
        public Task AddAsync(DownloadHistoryEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync(SavedChannelEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(SavedChannelEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct) => Task.CompletedTask;
        public Task<LibraryEntry?> FindByPathAsync(string path, CancellationToken ct) => Task.FromResult<LibraryEntry?>(null);
        public Task UpsertAsync(LibraryEntry entry, CancellationToken ct) => Task.CompletedTask;
    }
}
