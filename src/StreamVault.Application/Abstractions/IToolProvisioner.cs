using StreamVault.Shared;

namespace StreamVault.Application.Abstractions;

/// <summary>Resolved absolute paths to the external tools the app drives.</summary>
public sealed record ToolPaths(string YtDlpPath, string FfmpegPath, string FfprobePath);

/// <summary>Status of a single external tool.</summary>
public sealed record ToolStatus(string Name, bool Available, string? Version, string Path);

/// <summary>Locates yt-dlp and ffmpeg, downloading official builds when missing so the app is self-contained.</summary>
public interface IToolProvisioner
{
    /// <summary>Currently resolved paths (may point at not-yet-provisioned locations until ensured).</summary>
    ToolPaths Paths { get; }

    bool IsReady { get; }

    /// <summary>Ensure both tools are present, provisioning (downloading) any that are missing.</summary>
    Task<Result<ToolPaths>> EnsureAsync(IProgress<string>? log = null, CancellationToken ct = default);

    Task<IReadOnlyList<ToolStatus>> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Update yt-dlp to the latest release.</summary>
    Task<Result> UpdateYtDlpAsync(IProgress<string>? log = null, CancellationToken ct = default);
}
