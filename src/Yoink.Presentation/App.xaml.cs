using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yoink.Application.Abstractions;
using Yoink.Infrastructure;
using Yoink.Presentation.Navigation;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels;

namespace Yoink.Presentation;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterGlobalExceptionHandlers();

        try
        {
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            _host.Start();

            var services = _host.Services;

            // Load settings and prepare the database before any page binds to data.
            var settings = services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();
            AppLog.Verbose = settings.Current.DiagnosticLogging || settings.Current.VerboseOutput;
            await services.InitializeYoinkDatabaseAsync();

            var mainWindow = services.GetRequiredService<MainWindow>();
            services.GetRequiredService<ShellViewModel>().Initialize();
            mainWindow.Show();

            AppLog.Info("Yoink started.");

            // Provision yt-dlp/ffmpeg in the background so first launch isn't blocked.
            _ = ProvisionToolsAsync(services);
        }
        catch (Exception ex)
        {
            AppLog.Error("Fatal error during startup.", ex);
            MessageBox.Show(
                $"Yoink failed to start.\n\n{ex.Message}\n\nA log was written to:\n{AppLog.LogDirectory}",
                "Yoink", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe error was logged to:\n{AppLog.LogDirectory}",
                "Yoink", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true; // keep the app alive; the operation is aborted but the window survives
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppLog.Error("Unhandled non-UI exception.", args.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLog.Error("Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
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
        services.AddYoinkInfrastructure();
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
