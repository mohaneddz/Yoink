using Yoink.Application.Abstractions;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels;
using Yoink.Shared;

namespace Yoink.Tests;

public class UpdateButtonStateTests
{
    private static ToolUpdateInfo Info(bool available, string current = "2026.01.01") =>
        new(available, current, "2026.02.01", available, "ffmpeg version n7.0", false, DateTimeOffset.UtcNow);

    private static SettingsViewModel Create(ControlledTools tools)
    {
        var settings = new TestSettings();
        var coordinator = new DownloadCoordinator(null!, null!, null!, null!, null!, settings, null!);
        return new SettingsViewModel(settings, tools, null!, coordinator, new NoStartupChanges());
    }

    [Fact]
    public async Task Checking_disables_the_action_and_then_shows_the_available_update()
    {
        var completed = new TaskCompletionSource<ToolUpdateInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tools = new ControlledTools { Check = () => completed.Task };
        var vm = Create(tools);
        var operation = vm.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.Equal("CHECKING…", vm.UpdateButtonText);
        Assert.True(vm.IsToolsBusy);
        Assert.False(vm.CheckUpdatesCommand.CanExecute(null));
        completed.SetResult(Info(true));
        await operation;
        Assert.Equal("UPDATE NOW", vm.UpdateButtonText);
        Assert.False(vm.IsToolsBusy);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Updating_has_a_busy_label_and_a_failed_update_can_be_retried()
    {
        var completed = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tools = new ControlledTools { LastUpdateInfo = Info(true), Update = () => completed.Task };
        var vm = Create(tools);
        var operation = vm.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.Equal("UPDATING…", vm.UpdateButtonText);
        Assert.True(vm.IsUpdatingTools);
        Assert.False(vm.CheckUpdatesCommand.CanExecute(null));
        completed.SetResult(Result.Failure("Download unavailable"));
        await operation;
        Assert.Equal("UPDATE NOW", vm.UpdateButtonText);
        Assert.False(vm.IsToolsBusy);
        Assert.False(vm.IsUpdatingTools);
        Assert.Contains("Update failed", vm.StatusMessage);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Successful_update_restores_the_check_action_and_latest_badge()
    {
        var tools = new ControlledTools { LastUpdateInfo = Info(true) };
        tools.Update = () =>
        {
            tools.Version = "2026.02.01";
            tools.LastUpdateInfo = Info(false, tools.Version);
            return Task.FromResult(Result.Success());
        };
        var vm = Create(tools);
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.False(vm.HasUpdate);
        Assert.Equal("CHECK UPDATES", vm.UpdateButtonText);
        Assert.Equal("LATEST", vm.YtDlpBadgeText);
    }

    [Fact]
    public async Task Check_failure_restores_the_action_and_reports_the_error()
    {
        var vm = Create(new ControlledTools { Check = () => throw new InvalidOperationException("Offline") });
        await vm.CheckUpdatesCommand.ExecuteAsync(null);
        Assert.False(vm.IsToolsBusy);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
        Assert.Contains("Offline", vm.StatusMessage);
    }

    [Fact]
    public void Startup_probe_failure_does_not_leave_the_button_stuck_busy()
    {
        var vm = Create(new ControlledTools { FailProbe = true });
        Assert.False(vm.IsToolsBusy);
        Assert.True(vm.CheckUpdatesCommand.CanExecute(null));
        Assert.Equal("UNKNOWN", vm.YtDlpBadgeText);
        Assert.Contains("Could not load tool information", vm.StatusMessage);
    }

    [Fact]
    public void Missing_tools_are_not_labeled_latest_or_ready()
    {
        var vm = Create(new ControlledTools { Missing = true });
        Assert.Equal("MISSING", vm.YtDlpBadgeText);
        Assert.Equal("MISSING", vm.FfmpegBadgeText);
        Assert.False(vm.IsYtDlpUpdated);
    }

    [Fact]
    public void A_changed_installed_version_rechecks_stale_update_information()
    {
        var tools = new ControlledTools { Version = "2026.02.01", LastUpdateInfo = Info(true) };
        tools.Check = () => Task.FromResult(Info(false, tools.Version));
        var vm = Create(tools);
        Assert.Equal(1, tools.Checks);
        Assert.False(vm.HasUpdate);
        Assert.Equal("CHECK UPDATES", vm.UpdateButtonText);
    }

    private sealed class NoStartupChanges : IWindowsStartupService
    {
        public void Apply(bool enabled, bool minimized) { }
    }

    private sealed class ControlledTools : TestTools
    {
        public string Version { get; set; } = "2026.01.01";
        public bool Missing { get; init; }
        public bool FailProbe { get; init; }
        public int Checks { get; private set; }
        public Func<Task<ToolUpdateInfo>>? Check { get; set; }
        public Func<Task<Result>>? Update { get; set; }
        public ControlledTools() => LastUpdateInfo = Info(false);
        public override Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken ct = default)
        {
            if (FailProbe) throw new InvalidOperationException("Probe failed");
            return Task.FromResult<IReadOnlyList<ToolStatus>>(Missing ? Array.Empty<ToolStatus>() : new[]
            {
                new ToolStatus("yt-dlp", true, Version, "yt-dlp.exe"),
                new ToolStatus("ffmpeg", true, "ffmpeg version n7.0", "ffmpeg.exe")
            });
        }
        public override Task<ToolUpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            Checks++;
            return Check?.Invoke() ?? Task.FromResult(LastUpdateInfo);
        }
        public override Task<Result> UpdateAllToolsAsync(IProgress<string>? log = null, CancellationToken ct = default) =>
            Update?.Invoke() ?? Task.FromResult(Result.Success());
    }
}
