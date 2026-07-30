using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StreamVault.Application.Abstractions;
using StreamVault.Application.Downloads;
using StreamVault.Domain.Entities;
using StreamVault.Presentation.Navigation;
using StreamVault.Presentation.Services;
using StreamVault.Presentation.ViewModels.Models;
using StreamVault.Shared;

namespace StreamVault.Presentation.ViewModels;

/// <summary>Backs the History page: a persisted ledger of download attempts with retry/export.</summary>
public partial class HistoryViewModel : ObservableObject
{
    private readonly InspectorViewModel _inspector;
    private readonly IHistoryRepository _repository;
    private readonly ISystemAccess _system;
    private readonly DownloadCoordinator _coordinator;
    private readonly INavigationService _navigation;
    private readonly ISettingsService _settings;
    private readonly List<HistoryRecord> _all = new();

    public HistoryViewModel(
        InspectorViewModel inspector,
        IHistoryRepository repository,
        ISystemAccess system,
        DownloadCoordinator coordinator,
        INavigationService navigation,
        ISettingsService settings)
    {
        _inspector = inspector;
        _repository = repository;
        _system = system;
        _coordinator = coordinator;
        _navigation = navigation;
        _settings = settings;
        _ = LoadAsync();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _dateRangeText = "All time";
    [ObservableProperty] private string _resultFilter = "All Results";
    [ObservableProperty] private string _sourceTypeFilter = "All Sources";
    [ObservableProperty] private string _formatFilter = "All Formats";

    public ObservableCollection<HistoryRecord> Records { get; } = new();
    public ObservableCollection<string> ResultFilterOptions { get; } =
        new() { "All Results", "Success", "Failed", "Cancelled", "Partial" };
    public ObservableCollection<string> SourceTypeOptions { get; } =
        new() { "All Sources", "YouTube", "Vimeo", "Other" };
    public ObservableCollection<string> FormatOptions { get; } = new() { "All Formats" };

    [ObservableProperty] private HistoryRecord? _selectedRecord;

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnResultFilterChanged(string value) => ApplyFilters();
    partial void OnSourceTypeFilterChanged(string value) => ApplyFilters();
    partial void OnFormatFilterChanged(string value) => ApplyFilters();

    partial void OnSelectedRecordChanged(HistoryRecord? value)
    {
        if (value is not null)
        {
            _inspector.Show(value);
        }
    }

    public async Task LoadAsync()
    {
        var entries = await _repository.GetAllAsync();
        _all.Clear();
        var i = 1;
        foreach (var e in entries)
        {
            _all.Add(Map(e, i++));
        }

        FormatOptions.Clear();
        FormatOptions.Add("All Formats");
        foreach (var fmt in _all.Select(r => r.Format).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())
        {
            FormatOptions.Add(fmt);
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<HistoryRecord> query = _all;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Channel.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.SourceUrl.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (ResultFilter != "All Results" && Enum.TryParse<HistoryResult>(ResultFilter, out var res))
        {
            query = query.Where(r => r.Result == res);
        }

        if (SourceTypeFilter != "All Sources")
        {
            query = query.Where(r => MatchesSource(r.SourceUrl, SourceTypeFilter));
        }

        if (FormatFilter != "All Formats")
        {
            query = query.Where(r => r.Format == FormatFilter);
        }

        Records.Clear();
        foreach (var r in query)
        {
            Records.Add(r);
        }
    }

    private static bool MatchesSource(string url, string filter) => filter switch
    {
        "YouTube" => url.Contains("youtu", StringComparison.OrdinalIgnoreCase),
        "Vimeo" => url.Contains("vimeo", StringComparison.OrdinalIgnoreCase),
        "Other" => !url.Contains("youtu", StringComparison.OrdinalIgnoreCase) && !url.Contains("vimeo", StringComparison.OrdinalIgnoreCase),
        _ => true
    };

    [RelayCommand]
    private void RetrySelected()
    {
        if (SelectedRecord is null || string.IsNullOrWhiteSpace(SelectedRecord.SourceUrl))
        {
            return;
        }

        Reenqueue(SelectedRecord, start: true);
        SelectedRecord.Redownloaded = true;
        _navigation.Navigate(AppPage.Queue);
    }

    [RelayCommand]
    private void ReuseSettings()
    {
        if (SelectedRecord is null || string.IsNullOrWhiteSpace(SelectedRecord.SourceUrl))
        {
            return;
        }

        Reenqueue(SelectedRecord, start: false);
        _navigation.Navigate(AppPage.Queue);
    }

    private void Reenqueue(HistoryRecord record, bool start)
    {
        var settings = _settings.Current;
        var request = new DownloadRequest
        {
            Url = record.SourceUrl,
            Kind = record.Kind,
            VideoContainer = settings.PreferredVideoContainer,
            AudioContainer = settings.PreferredAudioContainer,
            OutputFolder = string.IsNullOrWhiteSpace(record.FilePath)
                ? settings.DefaultDownloadFolder
                : Path.GetDirectoryName(record.FilePath) ?? settings.DefaultDownloadFolder,
            FilenameTemplate = settings.FilenameTemplate,
            EmbedMetadata = true,
            CookiesFile = settings.CookiesFile,
            Proxy = settings.Proxy,
            SpeedLimitKbps = settings.DownloadSpeedLimitKbps,
            KnownTitle = record.Title
        };

        _coordinator.Enqueue(request, new EnqueueInfo(record.Title, record.Channel, record.ResolutionText, record.Format), start);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var path = SelectedRecord?.FilePath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            _system.RevealInExplorer(path);
        }
    }

    [RelayCommand]
    private void CopySourceUrl()
    {
        if (!string.IsNullOrWhiteSpace(SelectedRecord?.SourceUrl))
        {
            _system.SetClipboardText(SelectedRecord.SourceUrl);
        }
    }

    [RelayCommand]
    private void ExportLogs()
    {
        if (SelectedRecord is null)
        {
            return;
        }

        var file = _system.PickFile("Log files (*.log)|*.log|Text files (*.txt)|*.txt");
        if (file is not null)
        {
            File.WriteAllText(file, string.Join(Environment.NewLine, SelectedRecord.LogLines));
        }
    }

    [RelayCommand]
    private void ExportHistory()
    {
        var file = _system.PickFile("CSV files (*.csv)|*.csv");
        if (file is null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("Title,Channel,Timestamp,Result,Size,Format,Output");
        foreach (var r in _all)
        {
            sb.AppendLine($"{Csv(r.Title)},{Csv(r.Channel)},{Csv(r.TimestampText)},{r.Result},{Csv(r.FileSizeText)},{Csv(r.Format)},{Csv(r.FilePath)}");
        }

        File.WriteAllText(file, sb.ToString());
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    [RelayCommand]
    private void ClearFilters()
    {
        ResultFilter = "All Results";
        SourceTypeFilter = "All Sources";
        FormatFilter = "All Formats";
        SearchText = string.Empty;
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        await _repository.ClearAsync();
        _all.Clear();
        Records.Clear();
    }

    private static HistoryRecord Map(DownloadHistoryEntry e, int index)
    {
        var record = new HistoryRecord
        {
            EntryId = e.Id,
            Index = index,
            Title = e.Title,
            Channel = e.Channel,
            TimestampText = Formatting.ShortDate(e.Timestamp),
            Result = MapResult(e.Outcome),
            Output = string.IsNullOrWhiteSpace(e.OutputPath) ? "—" : Path.GetDirectoryName(e.OutputPath) ?? e.OutputPath,
            FilePath = e.OutputPath,
            FileSizeText = e.FileSizeBytes > 0 ? Formatting.Bytes(e.FileSizeBytes) : "—",
            Format = string.IsNullOrWhiteSpace(e.Format) ? "—" : e.Format,
            DurationText = Formatting.Duration(e.DurationSeconds),
            Redownloaded = e.Redownloaded,
            SourceUrl = e.SourceUrl,
            ResolutionText = string.IsNullOrWhiteSpace(e.ResolutionText) ? "—" : e.ResolutionText,
            VideoCodecText = string.IsNullOrWhiteSpace(e.VideoCodec) ? "—" : e.VideoCodec,
            AudioCodecText = string.IsNullOrWhiteSpace(e.AudioCodec) ? "—" : e.AudioCodec,
            Fps = (int)Math.Round(e.Fps ?? 0),
            CompletedText = Formatting.ShortDate(e.Timestamp),
            ExitCodeText = e.Outcome == DownloadOutcome.Success ? "0 (OK)" : $"{e.ExitCode} (Error)",
            Kind = e.Kind
        };

        foreach (var line in (e.LogText ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            record.LogLines.Add(line.TrimEnd());
        }

        return record;
    }

    private static HistoryResult MapResult(DownloadOutcome outcome) => outcome switch
    {
        DownloadOutcome.Success => HistoryResult.Success,
        DownloadOutcome.Failed => HistoryResult.Failed,
        DownloadOutcome.Cancelled => HistoryResult.Cancelled,
        DownloadOutcome.Skipped => HistoryResult.Skipped,
        DownloadOutcome.Partial => HistoryResult.Partial,
        _ => HistoryResult.Failed
    };
}
