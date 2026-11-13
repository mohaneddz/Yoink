using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Yoink.Application.Abstractions;
using Yoink.Application.Downloads;
using Yoink.Domain.Entities;
using Yoink.Domain.Settings;
using Yoink.Presentation.ViewModels.Models;
using Yoink.Shared;

namespace Yoink.Presentation.Services;

/// <summary>Display metadata attached to a queued job (independent of the yt-dlp request).</summary>
public sealed record EnqueueInfo(
    string Title,
    string Channel,
    string Quality,
    string Format,
    string? ThumbnailUrl = null,
    long? EstimatedSizeBytes = null);

/// <summary>
/// Central download queue: owns the live <see cref="QueueJob"/> collection, runs jobs with a
/// configurable concurrency limit, streams progress to the UI, and records history/library results.
/// Shared across the Download, Queue, Playlists, and Channels pages.
/// </summary>
public sealed class DownloadCoordinator
{
    private sealed class JobRuntime
    {
        public required DownloadRequest Request { get; init; }
        public CancellationTokenSource? Cts { get; set; }
        public bool Running { get; set; }
        public DownloadKind Kind { get; init; }
    }

    private readonly IDownloadEngine _engine;
    private readonly IHistoryRepository _history;
    private readonly ILibraryRepository _library;
    private readonly IQueueRepository _queue;
    private readonly IFfmpegService _ffmpeg;
    private readonly ISettingsService _settings;
    private readonly ISystemAccess _system;
    private readonly ConcurrentDictionary<QueueJob, JobRuntime> _runtimes = new();
    private readonly ConcurrentDictionary<QueueJob, double> _speeds = new();

    private int _orderCounter;

    public DownloadCoordinator(
        IDownloadEngine engine,
        IHistoryRepository history,
        ILibraryRepository library,
        IQueueRepository queue,
        IFfmpegService ffmpeg,
        ISettingsService settings,
        ISystemAccess system)
    {
        _engine = engine;
        _history = history;
        _library = library;
        _queue = queue;
        _ffmpeg = ffmpeg;
        _settings = settings;
        _system = system;
    }

    /// <summary>
    /// Loads persisted queue rows from a previous session. Call once at startup, before the Queue
    /// page can be shown. Jobs that were actively running when the app last closed are restored as
    /// Paused (the process itself did not survive) so the user can resume them explicitly.
    /// </summary>
    public async Task RestoreAsync()
    {
        IReadOnlyList<QueueJobEntity> saved;
        try
        {
            saved = await _queue.GetAllAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load persisted queue.", ex);
            return;
        }

        foreach (var entity in saved)
        {
            DownloadRequest request;
            try
            {
                request = JsonSerializer.Deserialize<DownloadRequest>(entity.RequestJson)
                    ?? throw new InvalidOperationException("Empty request payload.");
            }
            catch (Exception ex)
            {
                AppLog.Error($"Skipped unrestorable queue row #{entity.Id} ({entity.Title}).", ex);
                continue;
            }

            var wasActive = IsActive(MapPersistedStatus(entity.Status));
            var job = new QueueJob
            {
                Order = entity.Order,
                Title = entity.Title,
                Channel = entity.Channel,
                Format = entity.Format,
                Quality = entity.Quality,
                SizeText = entity.SizeText,
                Destination = entity.Destination,
                OutputFolder = entity.OutputFolder,
                SelectedFormatText = entity.SelectedFormatText,
                ThumbnailHex = string.IsNullOrEmpty(entity.ThumbnailHex) ? ColorFromKey(entity.Title) : entity.ThumbnailHex,
                ThumbnailUrl = entity.ThumbnailUrl ?? string.Empty,
                SourceUrl = entity.SourceUrl,
                FileSizeText = entity.FileSizeText,
                DownloadedText = entity.DownloadedText,
                Notes = entity.Notes,
                Progress = entity.Progress,
                RetryCount = entity.RetryCount,
                Status = wasActive ? QueueJobStatus.Paused : MapPersistedStatus(entity.Status),
                StatusDetailText = wasActive ? "Paused — interrupted by app restart" : entity.StatusDetailText,
                AddedAtText = Formatting.ShortDate(entity.AddedAt),
                LastUpdateText = entity.LastUpdateAt is { } lu ? Formatting.ShortDate(lu) : string.Empty,
                PersistedId = entity.Id
            };
            job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Restored from previous session");
            job.ResumeAction = () => ResumeJob(job);
            job.OpenFolderAction = () => OpenJobFolder(job);

            _runtimes[job] = new JobRuntime { Request = request, Kind = entity.Kind };
            Jobs.Add(job);
            _orderCounter = Math.Max(_orderCounter, entity.Order);
        }

        JobsChanged?.Invoke(this, EventArgs.Empty);
        Pump();
    }

    /// <summary>The live queue, bound directly by the Queue page. Mutated on the UI thread.</summary>
    public ObservableCollection<QueueJob> Jobs { get; } = new();

    public event EventHandler? JobsChanged;

    /// <summary>Combined download speed of all currently-active jobs, in bytes/second.</summary>
    public double AggregateBytesPerSecond => _speeds.Sum(kv => kv.Value);

    public int ActiveJobCount => Jobs.Count(j => j.Status is QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio
        or QueueJobStatus.DownloadingSubtitles or QueueJobStatus.Merging or QueueJobStatus.Converting
        or QueueJobStatus.WritingMetadata or QueueJobStatus.Retrying);

    public int PendingJobCount => Jobs.Count(j => j.Status is not (QueueJobStatus.Completed or QueueJobStatus.Cancelled));

    public QueueJob Enqueue(DownloadRequest request, EnqueueInfo info, bool startImmediately)
    {
        var job = new QueueJob
        {
            Order = ++_orderCounter,
            Title = info.Title,
            Channel = info.Channel,
            Quality = info.Quality,
            Format = info.Format,
            SizeText = info.EstimatedSizeBytes is { } b ? Formatting.Bytes(b) : "—",
            FileSizeText = info.EstimatedSizeBytes is { } b2 ? Formatting.Bytes(b2) : "—",
            Status = QueueJobStatus.Queued,
            SourceUrl = request.Url,
            OutputFolder = request.OutputFolder,
            Destination = request.OutputFolder,
            SelectedFormatText = $"{info.Format} · {info.Quality}",
            ThumbnailHex = ColorFromKey(info.Title),
            ThumbnailUrl = info.ThumbnailUrl ?? string.Empty,
            AddedAtText = Formatting.ShortDate(DateTimeOffset.Now)
        };
        job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Queued: {info.Title}");
        job.ResumeAction = () => ResumeJob(job);
        job.OpenFolderAction = () => OpenJobFolder(job);

        _runtimes[job] = new JobRuntime { Request = request, Kind = request.Kind };
        if (!startImmediately)
        {
            job.Status = QueueJobStatus.Paused;
            job.StatusDetailText = "Paused";
        }

        Jobs.Add(job);
        JobsChanged?.Invoke(this, EventArgs.Empty);
        Persist(job, request, request.Kind);

        if (startImmediately)
        {
            Pump();
        }

        return job;
    }

    /// <summary>Re-evaluate the queue (e.g. after the concurrency limit changed).</summary>
    public void Refresh() => Pump();

    /// <summary>
    /// Re-checks that completed downloads still exist on disk and flags any that were removed or
    /// moved externally. Cheap enough to call on a short UI timer so the Queue page stays honest.
    /// </summary>
    public void VerifyFilesExist()
    {
        foreach (var job in Jobs)
        {
            if (job.Status == QueueJobStatus.Completed)
            {
                var path = job.Destination;
                job.FileMissing = !string.IsNullOrWhiteSpace(path) && !File.Exists(path) && !Directory.Exists(path);
            }
            else if (job.FileMissing)
            {
                job.FileMissing = false;
            }
        }
    }

    private void OpenJobFolder(QueueJob job)
    {
        var target = !string.IsNullOrWhiteSpace(job.Destination) && File.Exists(job.Destination)
            ? Path.GetDirectoryName(job.Destination)
            : job.OutputFolder;
        if (!string.IsNullOrWhiteSpace(target))
        {
            _system.OpenFolder(target);
        }
    }

    public void StartAll()
    {
        foreach (var job in Jobs)
        {
            if (job.Status is QueueJobStatus.Paused or QueueJobStatus.Failed or QueueJobStatus.Cancelled or QueueJobStatus.Waiting)
            {
                job.Status = QueueJobStatus.Queued;
            }
        }

        Pump();
    }

    public void PauseAll()
    {
        foreach (var job in Jobs.ToList())
        {
            if (IsActive(job.Status))
            {
                PauseJob(job);
            }
        }
    }

    /// <summary>Cancels every running process and discards those jobs entirely (used by "Cancel Downloads and Exit").</summary>
    public void CancelActiveJobs()
    {
        // RemoveJob() cancels the job's CancellationTokenSource before removing it, so this both
        // stops the underlying yt-dlp process and drops the (now-discarded) job from the queue.
        foreach (var job in Jobs.Where(j => IsActive(j.Status)).ToList())
        {
            RemoveJob(job);
        }
    }

    public void StartJob(QueueJob job)
    {
        if (job.Status is not (QueueJobStatus.Completed or QueueJobStatus.DownloadingVideo))
        {
            job.Status = QueueJobStatus.Queued;
            Pump();
        }
    }

    public void PauseJob(QueueJob job)
    {
        if (_runtimes.TryGetValue(job, out var rt) && rt.Running)
        {
            rt.Cts?.Cancel();
            job.Status = QueueJobStatus.Paused;
            job.StatusDetailText = "Paused by user";
            job.SpeedText = "—";
            job.EtaText = "—";
        }
        else if (job.Status is QueueJobStatus.Queued)
        {
            job.Status = QueueJobStatus.Paused;
        }

        PersistExisting(job);
    }

    public void ResumeJob(QueueJob job)
    {
        job.Status = QueueJobStatus.Queued;
        job.StatusDetailText = "Resuming…";
        PersistExisting(job);
        Pump();
    }

    public void ToggleJob(QueueJob job)
    {
        if (IsActive(job.Status))
        {
            PauseJob(job);
        }
        else
        {
            ResumeJob(job);
        }
    }

    public void RetryJob(QueueJob job)
    {
        job.RetryCount++;
        job.Progress = 0;
        job.Status = QueueJobStatus.Queued;
        job.StatusDetailText = "Retrying…";
        PersistExisting(job);
        Pump();
    }

    public void RemoveJob(QueueJob job)
    {
        if (_runtimes.TryRemove(job, out var rt))
        {
            rt.Cts?.Cancel();
        }

        Jobs.Remove(job);
        JobsChanged?.Invoke(this, EventArgs.Empty);
        DeletePersisted(job);
    }

    public void ClearCompleted()
    {
        foreach (var job in Jobs.Where(j => j.Status == QueueJobStatus.Completed).ToList())
        {
            _runtimes.TryRemove(job, out _);
            Jobs.Remove(job);
            DeletePersisted(job);
        }

        JobsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearFailed()
    {
        foreach (var job in Jobs.Where(j => j.Status is QueueJobStatus.Failed or QueueJobStatus.Cancelled).ToList())
        {
            _runtimes.TryRemove(job, out _);
            Jobs.Remove(job);
            DeletePersisted(job);
        }

        JobsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RemoveJobs(IEnumerable<QueueJob> jobs)
    {
        foreach (var job in jobs.ToList())
        {
            if (_runtimes.TryRemove(job, out var rt))
            {
                rt.Cts?.Cancel();
            }

            Jobs.Remove(job);
            DeletePersisted(job);
        }

        JobsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void PauseJobs(IEnumerable<QueueJob> jobs)
    {
        foreach (var job in jobs.ToList())
        {
            PauseJob(job);
        }
    }

    public void ResumeJobs(IEnumerable<QueueJob> jobs)
    {
        foreach (var job in jobs.ToList())
        {
            ResumeJob(job);
        }
    }

    public void RetryFailed()
    {
        foreach (var job in Jobs.Where(j => j.Status is QueueJobStatus.Failed or QueueJobStatus.Cancelled))
        {
            job.Status = QueueJobStatus.Queued;
        }

        Pump();
    }

    private static bool IsActive(QueueJobStatus s) => s is
        QueueJobStatus.DownloadingVideo or QueueJobStatus.DownloadingAudio or QueueJobStatus.DownloadingSubtitles
        or QueueJobStatus.Merging or QueueJobStatus.Converting or QueueJobStatus.WritingMetadata
        or QueueJobStatus.Retrying or QueueJobStatus.Waiting;

    private void Pump()
    {
        var limit = Math.Max(1, _settings.Current.SimultaneousDownloads);
        var active = _runtimes.Values.Count(r => r.Running);

        foreach (var job in Jobs)
        {
            if (active >= limit)
            {
                break;
            }

            if (job.Status == QueueJobStatus.Queued && _runtimes.TryGetValue(job, out var rt) && !rt.Running)
            {
                StartRun(job, rt);
                active++;
            }
        }
    }

    private void StartRun(QueueJob job, JobRuntime rt)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => StartRun(job, rt));
            return;
        }

        rt.Cts = new CancellationTokenSource();
        rt.Running = true;
        job.Status = QueueJobStatus.DownloadingVideo;
        job.StatusDetailText = "Starting…";
        job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Starting download");
        PersistExisting(job);

        var progress = new Progress<DownloadProgress>(p => ApplyProgress(job, p));

        _ = Task.Run(async () =>
        {
            DownloadResult result;
            try
            {
                result = await _engine.DownloadAsync(rt.Request, progress, rt.Cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                result = new DownloadResult { Outcome = DownloadOutcome.Cancelled, ExitCode = -1 };
            }
            catch (Exception ex)
            {
                result = new DownloadResult { Outcome = DownloadOutcome.Failed, ErrorMessage = ex.Message, ExitCode = -1 };
            }

            if (dispatcher is not null)
            {
                await dispatcher.InvokeAsync(() => OnJobFinished(job, rt, result));
            }
            else
            {
                OnJobFinished(job, rt, result);
            }
        });
    }

    private void ApplyProgress(QueueJob job, DownloadProgress p)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => ApplyProgress(job, p));
            return;
        }

        if (p.RawLine is not null)
        {
            job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  {p.RawLine}");
            if (job.LogLines.Count > 500)
            {
                job.LogLines.RemoveAt(0);
            }
        }

        if (job.Status != QueueJobStatus.Paused)
        {
            job.Status = MapStage(p.Stage);
        }

        if (p.Percent is { } pct)
        {
            job.Progress = pct;
        }

        if (p.SpeedBytesPerSecond is not null)
        {
            job.SpeedText = Formatting.Speed(p.SpeedBytesPerSecond);
            _speeds[job] = p.SpeedBytesPerSecond.Value;
        }

        if (p.EtaSeconds is not null)
        {
            job.EtaText = Formatting.Eta(p.EtaSeconds);
        }

        if (p.TotalBytes is { } total and > 0)
        {
            job.SizeText = Formatting.Bytes(total);
            job.FileSizeText = Formatting.Bytes(total);
            if (p.DownloadedBytes is { } dl)
            {
                job.DownloadedText = $"{Formatting.Bytes(dl)} / {Formatting.Bytes(total)}";
            }
        }

        if (p.Destination is not null)
        {
            job.Destination = p.Destination;
        }

        job.LastUpdateText = Formatting.ShortDate(DateTimeOffset.Now);
    }

    private async void OnJobFinished(QueueJob job, JobRuntime rt, DownloadResult result)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            await dispatcher.InvokeAsync(() => OnJobFinished(job, rt, result));
            return;
        }

        rt.Running = false;
        rt.Cts?.Dispose();
        rt.Cts = null;
        _speeds.TryRemove(job, out _);

        switch (result.Outcome)
        {
            case DownloadOutcome.Success:
                job.Status = QueueJobStatus.Completed;
                job.Progress = 100;
                job.SpeedText = "—";
                job.EtaText = "—";
                job.StatusDetailText = "Completed";
                job.Destination = result.OutputPath ?? job.Destination;
                if (result.FileSizeBytes > 0)
                {
                    job.SizeText = Formatting.Bytes(result.FileSizeBytes);
                    job.FileSizeText = Formatting.Bytes(result.FileSizeBytes);
                }

                job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Completed → {job.Destination}");
                await RecordSuccessAsync(job, rt, result);
                break;

            case DownloadOutcome.Cancelled:
                // Paused jobs are cancelled internally; keep them resumable rather than marking cancelled.
                if (job.Status != QueueJobStatus.Paused)
                {
                    job.Status = QueueJobStatus.Cancelled;
                    job.StatusDetailText = "Cancelled";
                }

                break;

            default:
                job.Status = QueueJobStatus.Failed;
                job.StatusDetailText = result.ErrorMessage ?? "Failed";
                job.LogLines.Add($"{DateTime.Now:HH:mm:ss}  Failed: {result.ErrorMessage}");
                await RecordFailureAsync(job, rt, result);
                break;
        }

        PersistExisting(job);
        JobsChanged?.Invoke(this, EventArgs.Empty);
        Pump();
    }

    private async Task RecordSuccessAsync(QueueJob job, JobRuntime rt, DownloadResult result)
    {
        try
        {
            var probe = result.OutputPath is not null ? await _ffmpeg.ProbeAsync(result.OutputPath) : null;
            if (rt.Kind == DownloadKind.Video && SourceVideoQuality.SourceKey(job.SourceUrl) is { } source)
            {
                var height = rt.Request.MaxHeight > 0 ? rt.Request.MaxHeight : probe?.Height ?? 0;
                if (height > 0)
                {
                    _settings.Current.SourceVideoQualities ??= new();
                    _settings.Current.SourceVideoQualities[source] = height;
                    await _settings.SaveAsync(_settings.Current);
                }
            }
            var resolution = probe is { Width: { } w, Height: { } h } ? $"{w}×{h}" : job.Quality;

            await _history.AddAsync(new DownloadHistoryEntry
            {
                SourceUrl = job.SourceUrl,
                Title = job.Title,
                Channel = job.Channel,
                Outcome = DownloadOutcome.Success,
                OutputPath = result.OutputPath ?? string.Empty,
                FileSizeBytes = result.FileSizeBytes,
                Format = job.Format,
                ResolutionText = resolution,
                VideoCodec = probe?.VideoCodec ?? string.Empty,
                AudioCodec = probe?.AudioCodec ?? string.Empty,
                DurationSeconds = probe?.DurationSeconds,
                Fps = probe?.Fps,
                ExitCode = 0,
                Kind = rt.Kind,
                LogText = string.Join(Environment.NewLine, job.LogLines)
            });

            if (result.OutputPath is not null && File.Exists(result.OutputPath))
            {
                await _library.UpsertAsync(new LibraryEntry
                {
                    FilePath = result.OutputPath,
                    Title = job.Title,
                    Channel = job.Channel,
                    SourceUrl = job.SourceUrl,
                    Kind = rt.Kind,
                    Format = job.Format,
                    Quality = job.Quality,
                    ThumbnailUrl = job.ThumbnailUrl ?? string.Empty,
                    ResolutionText = resolution,
                    VideoCodec = probe?.VideoCodec ?? string.Empty,
                    AudioCodec = probe?.AudioCodec ?? string.Empty,
                    Fps = probe?.Fps,
                    DurationSeconds = probe?.DurationSeconds,
                    FileSizeBytes = result.FileSizeBytes,
                    DownloadedAt = DateTimeOffset.Now
                });
            }
        }
        catch
        {
            // Persistence failures must not break the UI flow.
        }
    }

    private async Task RecordFailureAsync(QueueJob job, JobRuntime rt, DownloadResult result)
    {
        try
        {
            await _history.AddAsync(new DownloadHistoryEntry
            {
                SourceUrl = job.SourceUrl,
                Title = job.Title,
                Channel = job.Channel,
                Outcome = DownloadOutcome.Failed,
                Format = job.Format,
                ExitCode = result.ExitCode,
                Kind = rt.Kind,
                LogText = string.Join(Environment.NewLine, job.LogLines)
            });
        }
        catch
        {
            // ignore
        }
    }

    private static QueueJobStatus MapStage(DownloadStage stage) => stage switch
    {
        DownloadStage.Preparing => QueueJobStatus.Waiting,
        DownloadStage.DownloadingVideo => QueueJobStatus.DownloadingVideo,
        DownloadStage.DownloadingAudio => QueueJobStatus.DownloadingAudio,
        DownloadStage.DownloadingSubtitles => QueueJobStatus.DownloadingSubtitles,
        DownloadStage.DownloadingThumbnail => QueueJobStatus.DownloadingVideo,
        DownloadStage.Merging => QueueJobStatus.Merging,
        DownloadStage.Converting => QueueJobStatus.Converting,
        DownloadStage.WritingMetadata => QueueJobStatus.WritingMetadata,
        DownloadStage.EmbeddingThumbnail => QueueJobStatus.WritingMetadata,
        DownloadStage.Finished => QueueJobStatus.Completed,
        DownloadStage.Error => QueueJobStatus.Failed,
        _ => QueueJobStatus.DownloadingVideo
    };

    /// <summary>Fire-and-forget insert for a freshly-enqueued job (we already have its request/kind to hand).</summary>
    private void Persist(QueueJob job, DownloadRequest request, DownloadKind kind)
    {
        var entity = BuildEntity(job, request, kind);
        _ = PersistAsync(job, entity);
    }

    /// <summary>Fire-and-forget update for a job already tracked in <see cref="_runtimes"/>.</summary>
    private void PersistExisting(QueueJob job)
    {
        if (!_runtimes.TryGetValue(job, out var rt))
        {
            return;
        }

        var entity = BuildEntity(job, rt.Request, rt.Kind);
        _ = PersistAsync(job, entity);
    }

    private async Task PersistAsync(QueueJob job, QueueJobEntity entity)
    {
        try
        {
            job.PersistedId = await _queue.UpsertAsync(entity).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to persist queue job '{job.Title}'.", ex);
        }
    }

    private void DeletePersisted(QueueJob job)
    {
        if (job.PersistedId == 0)
        {
            return;
        }

        var id = job.PersistedId;
        _ = Task.Run(async () =>
        {
            try
            {
                await _queue.DeleteAsync(id).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to delete persisted queue job #{id}.", ex);
            }
        });
    }

    private static QueueJobEntity BuildEntity(QueueJob job, DownloadRequest request, DownloadKind kind) => new()
    {
        Id = job.PersistedId,
        Order = job.Order,
        Title = job.Title,
        Channel = job.Channel,
        Format = job.Format,
        Quality = job.Quality,
        SizeText = job.SizeText,
        Destination = job.Destination,
        OutputFolder = job.OutputFolder,
        SelectedFormatText = job.SelectedFormatText,
        ThumbnailHex = job.ThumbnailHex,
        ThumbnailUrl = job.ThumbnailUrl,
        SourceUrl = job.SourceUrl,
        FileSizeText = job.FileSizeText,
        DownloadedText = job.DownloadedText,
        Notes = job.Notes,
        StatusDetailText = job.StatusDetailText,
        Progress = job.Progress,
        RetryCount = job.RetryCount,
        Status = MapDomainStatus(job.Status),
        Kind = kind,
        LastUpdateAt = DateTimeOffset.Now,
        RequestJson = JsonSerializer.Serialize(request)
    };

    private static PersistedQueueStatus MapDomainStatus(QueueJobStatus status) =>
        Enum.Parse<PersistedQueueStatus>(status.ToString());

    private static QueueJobStatus MapPersistedStatus(PersistedQueueStatus status) =>
        Enum.Parse<QueueJobStatus>(status.ToString());

    /// <summary>Deterministic accent color for a job thumbnail placeholder, derived from its title.</summary>
    private static string ColorFromKey(string key)
    {
        string[] palette = { "#FF2B3542", "#FF3A2F4A", "#FF1F3A34", "#FF43302B", "#FF2E3A4D", "#FF3D2E3E" };
        var hash = 0;
        foreach (var c in key)
        {
            hash = (hash * 31 + c) & 0x7FFFFFFF;
        }

        return palette[hash % palette.Length];
    }
}
