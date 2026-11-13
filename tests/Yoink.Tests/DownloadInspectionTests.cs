using Yoink.Application.Abstractions;
using Yoink.Domain.Media;
using Yoink.Domain.Settings;
using Yoink.Presentation.ViewModels;
using Yoink.Shared;

namespace Yoink.Tests;

public class DownloadInspectionTests
{
    private static MediaMetadata Media(string url, string stream = "hls-original") => new()
    {
        Id = "synthetic", Title = "Synthetic media", WebpageUrl = url, SourceKind = MediaSourceKind.Video, DurationSeconds = 60,
        Formats = new[]
        {
            new MediaFormat { FormatId = "hd", Height = 1080, Extension = "mp4", VideoCodec = "avc1", AudioCodec = "aac", TotalBitrateKbps = 4000 },
            new MediaFormat { FormatId = stream, Height = 480, Extension = "mp4", VideoCodec = "avc1", AudioCodec = "aac", TotalBitrateKbps = 1200 }
        }
    };

    private static DownloadViewModel ViewModel(StubInspector inspector)
    {
        var settings = new StubSettings();
        settings.Current.SourceVideoQualities["example.com"] = 480;
        return new DownloadViewModel(inspector, null!, settings, null!, null!);
    }

    [Fact]
    public async Task Inspection_uses_real_badges_estimates_and_remembered_quality()
    {
        var vm = ViewModel(new StubInspector());
        vm.SourceUrl = "https://example.com/video";
        await vm.InspectCommand.ExecuteAsync(null);
        Assert.Equal("EXAMPLE.COM", vm.SourceDisplayName);
        Assert.Equal("1080p", vm.MaximumResolutionText);
        Assert.Equal(480, vm.SelectedVideoStream?.Height);
        Assert.StartsWith("≈ ", vm.SelectedVideoStream!.Size);
        Assert.Equal(2, vm.VideoStreams.Count);
    }

    [Fact]
    public async Task Refresh_preserves_quality_when_manifest_stream_identifiers_change()
    {
        var inspector = new StubInspector();
        var vm = ViewModel(inspector);
        vm.SourceUrl = "https://example.com/video";
        await vm.InspectCommand.ExecuteAsync(null);
        inspector.Lookup = (url, _, _) => Task.FromResult(Result<MediaMetadata>.Success(Media(url, "hls-refreshed")));
        await vm.RefreshStreamsCommand.ExecuteAsync(null);
        Assert.True(inspector.LastForceRefresh);
        Assert.Equal("hls-refreshed", vm.SelectedVideoStream?.FormatId);
        Assert.Equal(480, vm.SelectedVideoStream?.Height);
        Assert.Equal("480p", vm.Quality);
    }

    [Fact]
    public async Task Source_change_cancels_previous_lookup_and_ignores_its_late_result()
    {
        var completion = new TaskCompletionSource<Result<MediaMetadata>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inspector = new StubInspector { Lookup = (_, _, _) => completion.Task };
        var vm = ViewModel(inspector);
        vm.SourceUrl = "https://example.com/old";
        var first = vm.InspectCommand.ExecuteAsync(null);
        var firstToken = inspector.LastToken;
        Assert.False(vm.RefreshStreamsCommand.CanExecute(null));
        inspector.Lookup = (url, _, _) => Task.FromResult(Result<MediaMetadata>.Success(Media(url)));
        vm.SourceUrl = "https://different.example/new";
        Assert.True(firstToken.IsCancellationRequested);
        completion.SetResult(Result<MediaMetadata>.Success(Media("https://example.com/old")));
        await first;
        await vm.InspectCommand.ExecuteAsync(null);
        Assert.Equal("DIFFERENT.EXAMPLE", vm.SourceDisplayName);
        Assert.True(vm.HasInspectedMedia);
        Assert.True(vm.RefreshStreamsCommand.CanExecute(null));
    }

    [Fact]
    public async Task Empty_stream_catalog_is_retried_once_without_manual_refresh()
    {
        var calls = 0;
        var inspector = new StubInspector
        {
            Lookup = (url, _, _) => Task.FromResult(Result<MediaMetadata>.Success(++calls == 1
                ? new MediaMetadata { Id = "test", Title = "Synthetic media", WebpageUrl = url }
                : Media(url)))
        };
        var vm = ViewModel(inspector);
        vm.SourceUrl = "https://example.com/video";
        await vm.InspectCommand.ExecuteAsync(null);
        Assert.Equal(2, calls);
        Assert.Equal(2, vm.VideoStreams.Count);
        Assert.Equal(480, vm.SelectedVideoStream?.Height);
    }

    [Fact]
    public async Task Failed_refresh_keeps_the_existing_streams_and_selection()
    {
        var inspector = new StubInspector();
        var vm = ViewModel(inspector);
        vm.SourceUrl = "https://example.com/video";
        await vm.InspectCommand.ExecuteAsync(null);
        var selected = vm.SelectedVideoStream;
        inspector.Lookup = (_, _, _) => Task.FromResult(Result<MediaMetadata>.Failure("Temporary network failure"));
        await vm.RefreshStreamsCommand.ExecuteAsync(null);
        Assert.Same(selected, vm.SelectedVideoStream);
        Assert.Equal(2, vm.VideoStreams.Count);
        Assert.True(vm.HasInspectedMedia);
        Assert.False(vm.IsRefreshingStreams);
    }

    private sealed class StubInspector : IMediaInspector
    {
        public Func<string, CancellationToken, bool, Task<Result<MediaMetadata>>> Lookup { get; set; } =
            (url, _, _) => Task.FromResult(Result<MediaMetadata>.Success(Media(url)));
        public CancellationToken LastToken { get; private set; }
        public bool LastForceRefresh { get; private set; }
        public MediaSourceKind DetectKind(string url) => MediaSourceKind.Video;
        public Task<Result<MediaMetadata>> InspectAsync(string url, CancellationToken ct = default, bool forceRefresh = false)
        {
            LastToken = ct;
            LastForceRefresh = forceRefresh;
            return Lookup(url, ct, forceRefresh);
        }
        public Task<Result<PlaylistMetadata>> InspectPlaylistAsync(string url, int? limit = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class StubSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public event EventHandler? Changed { add { } remove { } }
        public Task LoadAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) => Task.CompletedTask;
    }
}
