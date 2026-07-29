using Yoink.Application.Abstractions;

namespace Yoink.Presentation.Services;

/// <summary>
/// Background periodic channel re-check (spec section 7.4 "Optional periodic checks"). Runs for the
/// life of the app, waking up every <see cref="PollInterval"/> to re-inspect any saved channel whose
/// own <c>CheckFrequencyHours</c> interval has elapsed since it was last checked.
/// </summary>
public sealed class ChannelWatcherService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly ISavedChannelRepository _repository;
    private readonly IMediaInspector _inspector;

    public ChannelWatcherService(ISavedChannelRepository repository, IMediaInspector inspector)
    {
        _repository = repository;
        _inspector = inspector;
    }

    /// <summary>Starts the background loop. Fire-and-forget: failures are logged, never thrown to the caller.</summary>
    public void Start(CancellationToken ct = default) => _ = RunLoopAsync(ct);

    private async Task RunLoopAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(InitialDelay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckDueChannelsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error("Channel watcher pass failed.", ex);
            }

            try
            {
                await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task CheckDueChannelsAsync(CancellationToken ct)
    {
        var channels = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.Now;

        foreach (var channel in channels)
        {
            if (string.IsNullOrWhiteSpace(channel.ChannelUrl))
            {
                continue;
            }

            var frequencyHours = channel.CheckFrequencyHours <= 0 ? 6 : channel.CheckFrequencyHours;
            var due = channel.LastCheckedAt is null || now - channel.LastCheckedAt.Value >= TimeSpan.FromHours(frequencyHours);
            if (!due)
            {
                continue;
            }

            try
            {
                var result = await _inspector.InspectPlaylistAsync(channel.ChannelUrl, limit: 10).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    channel.VideoCount = Math.Max(channel.VideoCount, result.Value!.Entries.Count);
                }
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to check channel '{channel.Name}'.", ex);
            }

            channel.LastCheckedAt = now;

            try
            {
                await _repository.UpdateAsync(channel, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to save check result for channel '{channel.Name}'.", ex);
            }
        }
    }
}
