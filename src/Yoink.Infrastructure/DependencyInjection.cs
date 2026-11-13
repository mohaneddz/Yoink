using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Yoink.Application.Abstractions;
using Yoink.Infrastructure.Ffmpeg;
using Yoink.Infrastructure.Persistence;
using Yoink.Infrastructure.Processes;
using Yoink.Infrastructure.Settings;
using Yoink.Infrastructure.Proxy;
using Yoink.Infrastructure.Tools;
using Yoink.Infrastructure.Ytdlp;

namespace Yoink.Infrastructure;

/// <summary>Registers all Infrastructure services (tools, yt-dlp/ffmpeg, persistence, settings).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddYoinkInfrastructure(this IServiceCollection services)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yoink");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "streamvault.db");

        services.AddHttpClient("tools", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Yoink", "1.0"));
        });

        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IToolProvisioner, ToolProvisioner>();
        services.AddSingleton<PreparedDownloadCache>();
        services.AddSingleton<IMediaInspector, YtDlpInspector>();
        services.AddSingleton<IDownloadEngine, YtDlpDownloadEngine>();
        services.AddSingleton<IFfmpegService, FfmpegService>();
        services.AddSingleton<TorManager>();
        services.AddSingleton<IProxyResolver, ProxyResolver>();

        services.AddDbContextFactory<YoinkDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<ILibraryRepository, LibraryRepository>();
        services.AddSingleton<ISavedChannelRepository, SavedChannelRepository>();
        services.AddSingleton<IQueueRepository, QueueRepository>();

        return services;
    }

    /// <summary>Create the SQLite database/schema if it does not yet exist and migrate missing columns.</summary>
    public static async Task InitializeYoinkDatabaseAsync(this IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<YoinkDbContext>>();
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);

        // Ensure backward-compatible migrations for any missing SQLite columns
        try
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Queue ADD COLUMN ThumbnailUrl TEXT DEFAULT ''").ConfigureAwait(false);
        }
        catch
        {
            // Column already exists or table does not exist yet
        }
    }
}
