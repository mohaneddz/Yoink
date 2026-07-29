using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.ViewModels;

namespace StreamVault.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(ConfigureServices)
            .Build();

        _host.Start();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<SystemStatusViewModel>();
        services.AddSingleton<StatusStripViewModel>(_ => new StatusStripViewModel
        {
            ActiveDownloads = 2,
            TotalSpeedMbps = 31.0,
            FilesInQueue = 5,
            StorageFreeTb = 1.23,
            Network = NetworkState.Stable
        });
        services.AddSingleton<InspectorViewModel>();

        services.AddTransient<DownloadViewModel>();
        services.AddTransient<QueueViewModel>();

        services.AddSingleton<Func<AppPage, object>>(provider => page => page switch
        {
            AppPage.Download => provider.GetRequiredService<DownloadViewModel>(),
            AppPage.Queue => provider.GetRequiredService<QueueViewModel>(),
            _ => new PlaceholderPageViewModel
            {
                Title = page.ToString().ToUpperInvariant(),
                Tagline = page switch
                {
                    AppPage.Playlists => "BATCH. ORGANIZE. DOWNLOAD.",
                    AppPage.Channels => "FOLLOW. REVIEW. QUEUE.",
                    AppPage.Library => "BROWSE. SEARCH. MANAGE.",
                    AppPage.History => "REVIEW. RETRY. AUDIT.",
                    AppPage.Settings => "CONFIGURE. OPTIMIZE. CONTROL.",
                    _ => string.Empty
                }
            }
        });

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<MainWindow>();
    }
}
