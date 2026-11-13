using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Yoink.Application.Abstractions;
using Yoink.Infrastructure;

namespace Yoink.Tests;

/// <summary>
/// Verifies the Infrastructure service graph resolves and the database initializes — the
/// runtime risks that a plain build does not catch.
/// </summary>
public class InfrastructureCompositionTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddYoinkInfrastructure();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void All_infrastructure_services_resolve()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<ISettingsService>());
        Assert.NotNull(provider.GetRequiredService<IToolProvisioner>());
        Assert.NotNull(provider.GetRequiredService<IMediaInspector>());
        Assert.NotNull(provider.GetRequiredService<IDownloadEngine>());
        Assert.NotNull(provider.GetRequiredService<IFfmpegService>());
        Assert.NotNull(provider.GetRequiredService<IProxyResolver>());
        Assert.NotNull(provider.GetRequiredService<IHistoryRepository>());
        Assert.NotNull(provider.GetRequiredService<ILibraryRepository>());
        Assert.NotNull(provider.GetRequiredService<ISavedChannelRepository>());
    }

    [Fact]
    public async Task Database_initializes_and_repositories_round_trip()
    {
        using var provider = BuildProvider();
        await provider.InitializeYoinkDatabaseAsync();

        var history = provider.GetRequiredService<IHistoryRepository>();
        var before = await history.GetAllAsync();

        await history.AddAsync(new Domain.Entities.DownloadHistoryEntry
        {
            Title = "DI smoke test entry",
            Channel = "tests",
            SourceUrl = "https://example.com/test",
            Outcome = Shared.DownloadOutcome.Success
        });

        var after = await history.GetAllAsync();
        Assert.Equal(before.Count + 1, after.Count);

        // Clean up so repeated runs stay deterministic.
        var added = after.First(e => e.Title == "DI smoke test entry");
        await history.DeleteAsync(added.Id);
    }

    [Fact]
    public void Detect_kind_classifies_common_urls()
    {
        using var provider = BuildProvider();
        var inspector = provider.GetRequiredService<IMediaInspector>();

        Assert.Equal(Shared.MediaSourceKind.Playlist, inspector.DetectKind("https://www.youtube.com/playlist?list=PL123"));
        Assert.Equal(Shared.MediaSourceKind.Short, inspector.DetectKind("https://www.youtube.com/shorts/abc123"));
        Assert.Equal(Shared.MediaSourceKind.Short, inspector.DetectKind("https://www.tiktok.com/@creator/video/1234567890"));
        Assert.Equal(Shared.MediaSourceKind.Short, inspector.DetectKind("https://www.instagram.com/reel/C8xyz123/"));
        Assert.Equal(Shared.MediaSourceKind.Channel, inspector.DetectKind("https://www.youtube.com/@SomeChannel"));
        Assert.Equal(Shared.MediaSourceKind.Video, inspector.DetectKind("https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        Assert.Equal(Shared.MediaSourceKind.Video, inspector.DetectKind("https://x.com/user/status/1234567890"));
        Assert.Equal(Shared.MediaSourceKind.Video, inspector.DetectKind("https://example.com/stream.m3u8"));
        Assert.Equal(Shared.MediaSourceKind.Unknown, inspector.DetectKind("not a url"));
    }
}
