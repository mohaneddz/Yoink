using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamVault.Application.Abstractions;
using StreamVault.Infrastructure;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels;

namespace StreamVault.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        _host.Start();

        var services = _host.Services;

        // Load settings and prepare the database before any page binds to data.
        await services.GetRequiredService<ISettingsService>().LoadAsync();
        await services.InitializeStreamVaultDatabaseAsync();

        var mainWindow = services.GetRequiredService<MainWindow>();
        services.GetRequiredService<ShellViewModel>().Initialize();
        mainWindow.Show();

        // Provision yt-dlp/ffmpeg in the background so first launch isn't blocked.
        _ = ProvisionToolsAsync(services);
    }

    private static async Task ProvisionToolsAsync(IServiceProvider services)
    {
        var status = services.GetRequiredService<SystemStatusViewModel>();
        var tools = services.GetRequiredService<IToolProvisioner>();

        status.State = SystemHealthState.Degraded;
        status.Detail = "Preparing yt-dlp and ffmpeg…";

        var log = new Progress<string>(msg => status.Detail = msg);
        var result = await tools.EnsureAsync(log);

        if (result.IsFailure)
        {
            status.State = SystemHealthState.MissingYtDlp;
            status.Detail = result.Error ?? "Tools unavailable";
            return;
        }

        var statuses = await tools.GetStatusAsync();
        var ytdlp = statuses.FirstOrDefault(s => s.Name == "yt-dlp");
        status.State = SystemHealthState.Operational;
        status.Detail = ytdlp?.Version is { } v ? $"yt-dlp {v} · ffmpeg ready" : "All systems normal";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddStreamVaultInfrastructure();
        services.AddSingleton<ISystemAccess, SystemAccess>();
        services.AddSingleton<DownloadCoordinator>();

        services.AddSingleton<SystemStatusViewModel>();
        services.AddSingleton<StatusStripViewModel>();
        services.AddSingleton<InspectorViewModel>();

        services.AddTransient<DownloadViewModel>();
        services.AddTransient<QueueViewModel>();
        services.AddTransient<PlaylistsViewModel>();
        services.AddTransient<ChannelsViewModel>();
        services.AddTransient<LibraryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<Func<AppPage, object>>(provider => page => page switch
        {
            AppPage.Download => provider.GetRequiredService<DownloadViewModel>(),
            AppPage.Queue => provider.GetRequiredService<QueueViewModel>(),
            AppPage.Playlists => provider.GetRequiredService<PlaylistsViewModel>(),
            AppPage.Channels => provider.GetRequiredService<ChannelsViewModel>(),
            AppPage.Library => provider.GetRequiredService<LibraryViewModel>(),
            AppPage.History => provider.GetRequiredService<HistoryViewModel>(),
            AppPage.Settings => provider.GetRequiredService<SettingsViewModel>(),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
        });

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<ShellViewModel>());
        services.AddSingleton<MainWindow>();
    }
}
