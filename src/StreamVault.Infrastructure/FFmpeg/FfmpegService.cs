using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StreamVault.Application.Abstractions;
using StreamVault.Infrastructure.Processes;
using StreamVault.Infrastructure.Ytdlp;
using StreamVault.Shared;

namespace StreamVault.Infrastructure.Ffmpeg;

/// <summary>Wraps ffprobe (probing) and ffmpeg (conversion/remux).</summary>
public sealed class FfmpegService : IFfmpegService
{
    private readonly ProcessRunner _runner;
    private readonly IToolProvisioner _tools;
    private readonly ILogger<FfmpegService> _logger;

    public FfmpegService(ProcessRunner runner, IToolProvisioner tools, ILogger<FfmpegService> logger)
    {
        _runner = runner;
        _tools = tools;
        _logger = logger;
    }

    public async Task<MediaProbe?> ProbeAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var ffprobe = _tools.Paths.FfprobePath;
        if (!File.Exists(ffprobe))
        {
            await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
            ffprobe = _tools.Paths.FfprobePath;
            if (!File.Exists(ffprobe))
            {
                return null;
            }
        }

        var args = new[]
        {
            "-v", "quiet", "-print_format", "json", "-show_format", "-show_streams", filePath
        };

        try
        {
            var result = await _runner.RunCaptureAsync(ffprobe, args, ct).ConfigureAwait(false);
            if (!result.Success || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(result.StandardOutput);
            var root = doc.RootElement;

            double? duration = null;
            long? size = null;
            if (root.TryGetProperty("format", out var format))
            {
                duration = format.GetDouble("duration");
                size = format.GetLong("size");
            }

            int? width = null, height = null;
            double? fps = null;
            string? vcodec = null, acodec = null;

            if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in streams.EnumerateArray())
                {
                    var type = s.GetStr("codec_type");
                    if (type == "video" && vcodec is null)
                    {
                        vcodec = s.GetStr("codec_name");
                        width = s.GetInt("width");
                        height = s.GetInt("height");
                        fps = ParseFrameRate(s.GetStr("avg_frame_rate") ?? s.GetStr("r_frame_rate"));
                    }
                    else if (type == "audio" && acodec is null)
                    {
                        acodec = s.GetStr("codec_name");
                    }
                }
            }

            return new MediaProbe(duration, width, height, fps, vcodec, acodec, size);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ffprobe failed for {File}", filePath);
            return null;
        }
    }

    public async Task<Result<string>> ConvertAsync(string inputPath, string targetContainer, IProgress<string>? log = null, CancellationToken ct = default)
    {
        if (!File.Exists(inputPath))
        {
            return Result<string>.Failure("Input file not found.");
        }

        var ensure = await _tools.EnsureAsync(ct: ct).ConfigureAwait(false);
        if (ensure.IsFailure)
        {
            return Result<string>.Failure(ensure.Error!);
        }

        var container = targetContainer.Trim().TrimStart('.').ToLowerInvariant();
        var outputPath = Path.Combine(
            Path.GetDirectoryName(inputPath)!,
            Path.GetFileNameWithoutExtension(inputPath) + "." + container);

        if (string.Equals(outputPath, inputPath, StringComparison.OrdinalIgnoreCase))
        {
            outputPath = Path.Combine(
                Path.GetDirectoryName(inputPath)!,
                Path.GetFileNameWithoutExtension(inputPath) + $"-converted.{container}");
        }

        // Try a fast stream-copy remux first; fall back to a full re-encode if that fails.
        var remux = await RunFfmpegAsync(new[] { "-y", "-i", inputPath, "-c", "copy", outputPath }, log, ct).ConfigureAwait(false);
        if (remux == 0 && File.Exists(outputPath))
        {
            return Result<string>.Success(outputPath);
        }

        log?.Report("Stream copy failed; re-encoding…");
        var encode = await RunFfmpegAsync(new[] { "-y", "-i", inputPath, outputPath }, log, ct).ConfigureAwait(false);
        return encode == 0 && File.Exists(outputPath)
            ? Result<string>.Success(outputPath)
            : Result<string>.Failure("Conversion failed.");
    }

    private async Task<int> RunFfmpegAsync(IEnumerable<string> args, IProgress<string>? log, CancellationToken ct)
    {
        try
        {
            return await _runner.RunStreamingAsync(
                _tools.Paths.FfmpegPath, args, null,
                line => log?.Report(line),
                line => log?.Report(line),
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ffmpeg run failed");
            return -1;
        }
    }

    private static double? ParseFrameRate(string? rate)
    {
        if (string.IsNullOrWhiteSpace(rate))
        {
            return null;
        }

        var parts = rate.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var num) &&
            double.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var den) &&
            den != 0)
        {
            return Math.Round(num / den, 2);
        }

        return double.TryParse(rate, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
