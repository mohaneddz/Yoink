using Yoink.Shared;

namespace Yoink.Application.Abstractions;

/// <summary>Resolved absolute paths to the external tools the app drives.</summary>
public sealed record ToolPaths(string YtDlpPath, string FfmpegPath, string FfprobePath);

/// <summary>Status of a single external tool.</summary>
public sealed record ToolStatus(string Name, bool Available, string? Version, string Path);

/// <summary>Information about available updates for external tools.</summary>
public sealed record ToolUpdateInfo(
    bool UpdateAvailable,
    string? CurrentYtDlpVersion,
    string? LatestYtDlpVersion,
    bool YtDlpUpdateAvailable,
    string? CurrentFfmpegVersion,
    bool FfmpegUpdateAvailable,
    DateTimeOffset LastChecked
);

/// <summary>Locates yt-dlp and ffmpeg, downloading official builds when missing so the app is self-contained.</summary>
public interface IToolProvisioner
{
    /// <summary>Currently resolved paths (may point at not-yet-provisioned locations until ensured).</summary>
    ToolPaths Paths { get; }

    bool IsReady { get; }

    /// <summary>Latest cached update check result.</summary>
    ToolUpdateInfo LastUpdateInfo { get; }

    /// <summary>Ensure both tools are present, provisioning (downloading) any that are missing.</summary>
    Task<Result<ToolPaths>> EnsureAsync(IProgress<string>? log = null, CancellationToken ct = default);

    Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Check online releases for yt-dlp and ffmpeg updates.</summary>
    Task<ToolUpdateInfo> CheckForUpdatesAsync(CancellationToken ct = default);

    /// <summary>Update all tools (yt-dlp and ffmpeg) to their latest versions.</summary>
    Task<Result> UpdateAllToolsAsync(IProgress<string>? log = null, CancellationToken ct = default);

    /// <summary>Update yt-dlp to the latest release.</summary>
    Task<Result> UpdateYtDlpAsync(IProgress<string>? log = null, CancellationToken ct = default);

    /// <summary>Update ffmpeg to the latest release.</summary>
    Task<Result> UpdateFfmpegAsync(IProgress<string>? log = null, CancellationToken ct = default);
}
