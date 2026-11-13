using System.Text.Json;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Entities;
using Yoink.Domain.Settings;
using Yoink.Presentation.Services;
using Yoink.Presentation.ViewModels;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Tests;

public class BackgroundBehaviorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Startup_recovery_only_resumes_interrupted_jobs(bool recover, bool expectedRunning)
    {
        var queue = new MemoryQueue
        {
            Items = new[] { Entry(1, PersistedQueueStatus.Waiting), Entry(2, PersistedQueueStatus.Paused) }
        };
        var coordinator = Coordinator(queue);
        await coordinator.RestoreAsync(recover);
        Assert.Equal(expectedRunning ? 1 : 0, coordinator.ActiveJobCount);
        Assert.Equal(QueueJobStatus.Paused, coordinator.Jobs.Single(job => job.PersistedId == 2).Status);
        Assert.Equal(expectedRunning ? QueueJobStatus.DownloadingVideo : QueueJobStatus.Paused,
            coordinator.Jobs.Single(job => job.PersistedId == 1).Status);
        coordinator.PauseAll();
    }

    [Fact]
    public void Preparing_download_is_counted_as_active_for_exit_and_sleep_protection()
    {
        var coordinator = Coordinator(new MemoryQueue());
        var job = coordinator.Enqueue(new DownloadRequest { Url = "https://example.com/video" }, new EnqueueInfo("Synthetic media", "", "480p", "MP4"), true);
        job.Status = QueueJobStatus.Waiting;
        Assert.Equal(1, coordinator.ActiveJobCount);
        coordinator.PauseAll();
    }

    [Fact]
    public async Task Immediate_resume_waits_for_the_old_process_to_cancel_then_restarts()
    {
        var engine = new ControlledEngine();
        var coordinator = new DownloadCoordinator(engine, null!, null!, new MemoryQueue(), null!, new TestSettings(), null!);
        var job = coordinator.Enqueue(new DownloadRequest { Url = "https://example.com/video" }, new EnqueueInfo("Synthetic media", "", "480p", "MP4"), true);
        await engine.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.PauseJob(job);
        coordinator.ResumeJob(job);
        engine.FirstResult.SetResult(new DownloadResult { Outcome = DownloadOutcome.Cancelled });
        await engine.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, coordinator.ActiveJobCount);
        Assert.Equal(QueueJobStatus.DownloadingVideo, job.Status);
        coordinator.PauseAll();
        engine.SecondResult.SetResult(new DownloadResult { Outcome = DownloadOutcome.Cancelled });
    }

    [Fact]
    public async Task Windows_startup_and_background_options_are_saved_together()
    {
        var settings = new TestSettings();
        var startup = new FakeStartup();
        var vm = new SettingsViewModel(settings, new TestTools(), null!, Coordinator(new MemoryQueue(), settings), startup)
        {
            StartWithWindows = true, StartMinimized = true, CloseToTray = true, MinimizeToTray = true,
            ResumeInterruptedDownloads = true, PreventSleepWhileDownloading = true, ConcurrentFragments = 16
        };
        await vm.SaveChangesCommand.ExecuteAsync(null);
        Assert.Equal((true, true), startup.Applied);
        Assert.True(settings.Current.StartWithWindows);
        Assert.True(settings.Current.StartMinimized);
        Assert.True(settings.Current.MinimizeToTray);
        Assert.True(settings.Current.ResumeInterruptedDownloads);
        Assert.Equal(16, settings.Current.ConcurrentFragments);
        Assert.Equal(1, settings.Saves);
    }

    [Fact]
    public async Task Startup_registration_failure_does_not_report_saved_settings()
    {
        var settings = new TestSettings();
        var vm = new SettingsViewModel(settings, new TestTools(), null!, Coordinator(new MemoryQueue(), settings), new FakeStartup { Fail = true })
        { StartWithWindows = true };
        await vm.SaveChangesCommand.ExecuteAsync(null);
        Assert.Equal(0, settings.Saves);
        Assert.False(settings.Current.StartWithWindows);
        Assert.Contains("Could not change Windows startup", vm.StatusMessage);
    }

    [Theory]
    [InlineData(false, "\"C:\\Program Files\\Yoink\\Yoink.exe\"")]
    [InlineData(true, "\"C:\\Program Files\\Yoink\\Yoink.exe\" --background")]
    public void Windows_startup_command_quotes_executable_paths(bool minimized, string expected) =>
        Assert.Equal(expected, WindowsStartupService.BuildCommand(@"C:\Program Files\Yoink\Yoink.exe", minimized));

    [Fact]
    public async Task A_second_launch_activates_the_existing_owner()
    {
        var name = @"Local\Yoink.Tests." + Guid.NewGuid().ToString("N");
        using (var first = new SingleInstanceService(name))
        using (var second = new SingleInstanceService(name))
        {
            Assert.True(first.IsPrimary);
            Assert.False(second.IsPrimary);
            var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            first.Listen(() => activated.TrySetResult());
            second.ActivateExisting();
            await activated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        using var next = new SingleInstanceService(name);
        Assert.True(next.IsPrimary);
    }

    private static QueueJobEntity Entry(int id, PersistedQueueStatus status) => new()
    {
        Id = id, Title = "Synthetic media", Status = status,
        RequestJson = JsonSerializer.Serialize(new DownloadRequest { Url = "https://example.com/video" })
    };
    private static DownloadCoordinator Coordinator(MemoryQueue queue, TestSettings? settings = null) =>
        new(new WaitingEngine(), null!, null!, queue, null!, settings ?? new TestSettings(), null!);

    private sealed class MemoryQueue : IQueueRepository
    {
        public IReadOnlyList<QueueJobEntity> Items { get; init; } = Array.Empty<QueueJobEntity>();
        public Task<IReadOnlyList<QueueJobEntity>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items);
        public Task<int> UpsertAsync(QueueJobEntity entry, CancellationToken ct = default) => Task.FromResult(entry.Id == 0 ? 1 : entry.Id);
        public Task DeleteAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class WaitingEngine : IDownloadEngine
    {
        public async Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new DownloadResult { Outcome = DownloadOutcome.Cancelled };
        }
    }
    private sealed class ControlledEngine : IDownloadEngine
    {
        private int _runs;
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DownloadResult> FirstResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<DownloadResult> SecondResult { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DownloadResult> DownloadAsync(DownloadRequest request, IProgress<DownloadProgress> progress, CancellationToken ct)
        {
            if (Interlocked.Increment(ref _runs) == 1) { FirstStarted.SetResult(); return FirstResult.Task; }
            SecondStarted.SetResult();
            return SecondResult.Task;
        }
    }
    private sealed class FakeStartup : IWindowsStartupService
    {
        public (bool, bool) Applied { get; private set; }
        public bool Fail { get; init; }
        public void Apply(bool enabled, bool minimized)
        {
            if (Fail) throw new InvalidOperationException("Simulated registry failure");
            Applied = (enabled, minimized);
        }
    }
}
