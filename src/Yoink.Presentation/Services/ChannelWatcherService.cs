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
    private readonly ISettingsService _settings;
    public event Action<string, string, int>? NewUploadsFound;

    public ChannelWatcherService(ISavedChannelRepository repository, IMediaInspector inspector, ISettingsService settings)
    {
        _repository = repository;
        _inspector = inspector;
        _settings = settings;
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
        if (!_settings.Current.EnableChannelChecks) return;
        var channels = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        var now = DateTimeOffset.Now;

        foreach (var channel in channels)
        {
            if (!_settings.Current.EnableChannelChecks) return;
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

            var newCount = 0;
            try
            {
                var result = await _inspector.InspectPlaylistAsync(UploadsUrl(channel.ChannelUrl), limit: 30).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    AppLog.Error($"Failed to check channel '{channel.Name}': {result.Error}");
                    continue;
                }
                var comparison = ChannelUploadTracker.Compare(channel.RecentUploadIdsJson, result.Value!.Entries);
                channel.RecentUploadIdsJson = comparison.Snapshot;
                newCount = comparison.NewCount;
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to check channel '{channel.Name}'.", ex);
                continue;
            }

            channel.LastCheckedAt = now;

            try
            {
                await _repository.UpdateAsync(channel, ct).ConfigureAwait(false);
                if (newCount > 0) NewUploadsFound?.Invoke(channel.Name, channel.ChannelUrl, newCount);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to save check result for channel '{channel.Name}'.", ex);
            }
        }
    }

    private static string UploadsUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !(uri.Host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
              uri.Host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase))) return url;
        var trimmed = url.TrimEnd('/');
        return trimmed.EndsWith("/videos", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "/videos";
    }
}
