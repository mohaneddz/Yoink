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
    private SingleInstanceService? _instance;
    private TrayService? _tray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterGlobalExceptionHandlers();

        try
        {
            _instance = new SingleInstanceService();
            if (!_instance.IsPrimary)
            {
                if (!e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase)) _instance.ActivateExisting();
                Shutdown();
                return;
            }
            _host = Host.CreateDefaultBuilder()
                .ConfigureServices(ConfigureServices)
                .Build();

            _host.Start();

            var services = _host.Services;

            // Load settings and prepare the database before any page binds to data.
            var settings = services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();
            try
            {
                services.GetRequiredService<IWindowsStartupService>().Apply(settings.Current.StartWithWindows, settings.Current.StartMinimized);
            }
            catch (Exception ex) { AppLog.Warn($"Could not update Windows startup: {ex.Message}"); }
            ThemeManager.ApplyTheme(settings.Current.Theme);
            AppLog.Verbose = settings.Current.DiagnosticLogging || settings.Current.VerboseOutput;
            await services.InitializeYoinkDatabaseAsync();
            await services.GetRequiredService<DownloadCoordinator>().RestoreAsync(settings.Current.ResumeInterruptedDownloads);

            var mainWindow = services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Closed += (_, _) => Shutdown();
            services.GetRequiredService<ShellViewModel>().Initialize();
            try { _tray = new TrayService(mainWindow, services.GetRequiredService<DownloadCoordinator>(), settings); }
            catch (Exception ex) { AppLog.Error("Could not create system tray icon.", ex); }
            var startHidden = settings.Current.StartMinimized || e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
            if (!startHidden || _tray is null) mainWindow.Show();
            _instance.Listen(() => Dispatcher.BeginInvoke(() =>
            {
                if (_tray is not null) _tray.ShowWindow();
                else { mainWindow.Show(); mainWindow.Activate(); }
            }));

            AppLog.Info("Yoink started.");

            // Provision yt-dlp/ffmpeg in the background so first launch isn't blocked.
            _ = ProvisionToolsAsync(services);

            // Periodic saved-channel re-checks (spec §7.4) run for the app's lifetime.
            services.GetRequiredService<ChannelWatcherService>().Start();
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

    private static DateTime _lastErrorDialogTime = DateTime.MinValue;

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            AppLog.Error("Unhandled UI exception.", args.Exception);
            var now = DateTime.UtcNow;
            if ((now - _lastErrorDialogTime).TotalSeconds > 3)
            {
                _lastErrorDialogTime = now;
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{args.Exception.Message}\n\nThe error was logged to:\n{AppLog.LogDirectory}",
                    "Yoink", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        // Check for updates in the background at launch
        _ = Task.Run(async () =>
        {
            try
            {
                var updateInfo = await tools.CheckForUpdatesAsync();
                if (updateInfo.UpdateAvailable)
                {
                    var dispatcher = Current?.Dispatcher;
                    if (dispatcher is not null)
                    {
                        dispatcher.Invoke(() =>
                        {
                            status.State = SystemHealthState.UpdateRequired;
                            status.Detail = updateInfo.YtDlpUpdateAvailable && !string.IsNullOrWhiteSpace(updateInfo.LatestYtDlpVersion)
                                ? $"Update available: yt-dlp {updateInfo.LatestYtDlpVersion}"
                                : "Tools update available";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Background update check error: {ex.Message}");
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _instance?.Dispose();
        _host?.StopAsync().GetAwaiter().GetResult();
        _host?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window) window.PrepareForSessionEnd();
        _host?.Services.GetRequiredService<DownloadCoordinator>().PauseAll();
        base.OnSessionEnding(e);
        Shutdown();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddYoinkInfrastructure();
        services.AddSingleton<ISystemAccess, SystemAccess>();
        services.AddSingleton<IWindowsStartupService, WindowsStartupService>();
        services.AddSingleton<DownloadCoordinator>();
        services.AddSingleton<ChannelWatcherService>();

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
