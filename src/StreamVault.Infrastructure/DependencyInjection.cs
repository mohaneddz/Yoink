using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StreamVault.Application.Abstractions;
using StreamVault.Infrastructure.Ffmpeg;
using StreamVault.Infrastructure.Persistence;
using StreamVault.Infrastructure.Processes;
using StreamVault.Infrastructure.Settings;
using StreamVault.Infrastructure.Tools;
using StreamVault.Infrastructure.Ytdlp;

namespace StreamVault.Infrastructure;

/// <summary>Registers all Infrastructure services (tools, yt-dlp/ffmpeg, persistence, settings).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStreamVaultInfrastructure(this IServiceCollection services)
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamVault");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "streamvault.db");

        services.AddHttpClient("tools", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StreamVault", "1.0"));
        });

        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IToolProvisioner, ToolProvisioner>();
        services.AddSingleton<IMediaInspector, YtDlpInspector>();
        services.AddSingleton<IDownloadEngine, YtDlpDownloadEngine>();
        services.AddSingleton<IFfmpegService, FfmpegService>();

        services.AddDbContextFactory<StreamVaultDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<ILibraryRepository, LibraryRepository>();
        services.AddSingleton<ISavedChannelRepository, SavedChannelRepository>();

        return services;
    }

    /// <summary>Create the SQLite database/schema if it does not yet exist.</summary>
    public static async Task InitializeStreamVaultDatabaseAsync(this IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<StreamVaultDbContext>>();
        await using var db = await factory.CreateDbContextAsync().ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }
}
