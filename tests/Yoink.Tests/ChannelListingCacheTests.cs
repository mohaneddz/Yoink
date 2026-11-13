using Yoink.Application.Abstractions;
using Yoink.Domain.Entities;
using Yoink.Domain.Media;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Tests;

public class ChannelListingCacheTests
{
    [Fact]
    public async Task Revisiting_a_channel_reuses_listings_but_refresh_and_context_changes_fetch_again()
    {
        var inspector = new Inspector();
        var settings = new TestSettings();
        var store = new Store();
        var vm = new ChannelsViewModel(store, inspector, null!, settings, null!, null!, store);
        var first = new SavedChannel { ChannelUrl = "https://example.com/first" };
        var second = new SavedChannel { ChannelUrl = "https://example.com/second" };
        await vm.SelectChannelCommand.ExecuteAsync(first);
        await vm.SelectChannelCommand.ExecuteAsync(second);
        Assert.Equal(8, inspector.Calls);
        await vm.SelectChannelCommand.ExecuteAsync(first);
        Assert.Equal(8, inspector.Calls);
        Assert.All(vm.Uploads, upload => Assert.StartsWith(first.ChannelUrl, upload.Url));
        await vm.RefreshUploadsCommand.ExecuteAsync(null);
        Assert.Equal(12, inspector.Calls);
        settings.Current.CookiesFile = "different-cookies.txt";
        await vm.SelectChannelCommand.ExecuteAsync(first);
        Assert.Equal(16, inspector.Calls);
        await vm.LoadMoreCommand.ExecuteAsync(null);
        Assert.Equal(20, inspector.Calls);
    }

    [Fact]
    public async Task A_failed_listing_is_not_reused_on_the_next_selection()
    {
        var inspector = new Inspector { Fail = true };
        var store = new Store();
        var vm = new ChannelsViewModel(store, inspector, null!, new TestSettings(), null!, null!, store);
        var channel = new SavedChannel { ChannelUrl = "https://example.com/channel" };
        await vm.SelectChannelCommand.ExecuteAsync(channel);
        Assert.Empty(vm.Uploads);
        inspector.Fail = false;
        await vm.SelectChannelCommand.ExecuteAsync(channel);
        Assert.Equal(8, inspector.Calls);
        Assert.NotEmpty(vm.Uploads);
        await vm.SelectChannelCommand.ExecuteAsync(channel);
        Assert.Equal(8, inspector.Calls);
    }

    private sealed class Inspector : IMediaInspector
    {
        public int Calls;
        public bool Fail;
        public MediaSourceKind DetectKind(string url) => MediaSourceKind.Channel;
        public Task<Result<MediaMetadata>> InspectAsync(string url, CancellationToken ct = default, bool forceRefresh = false) => throw new NotSupportedException();
        public Task<Result<PlaylistMetadata>> InspectPlaylistAsync(string url, int? limit = null, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(Fail ? Result<PlaylistMetadata>.Failure("Synthetic unavailable source")
                : Result<PlaylistMetadata>.Success(new PlaylistMetadata
                {
                    Id = url, WebpageUrl = url, Title = "Synthetic channel",
                    Entries = new[] { new Yoink.Domain.Media.PlaylistEntry { Id = url, Url = url + "/item", Title = "Synthetic upload" } }
                }));
        }
    }

    private sealed class Store : ILibraryRepository, ISavedChannelRepository
    {
        Task<IReadOnlyList<LibraryEntry>> ILibraryRepository.GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LibraryEntry>>(Array.Empty<LibraryEntry>());
        Task<IReadOnlyList<SavedChannelEntry>> ISavedChannelRepository.GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SavedChannelEntry>>(Array.Empty<SavedChannelEntry>());
        public Task<LibraryEntry?> FindByPathAsync(string path, CancellationToken ct) => Task.FromResult<LibraryEntry?>(null);
        public Task UpsertAsync(LibraryEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task AddAsync(SavedChannelEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(SavedChannelEntry entry, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(int id, CancellationToken ct) => Task.CompletedTask;
    }
}
