using System.Diagnostics;
using System.Text;

namespace Yoink.Infrastructure.Processes;

/// <summary>Result of a fully-captured process run.</summary>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Success => ExitCode == 0;
}

/// <summary>Runs external console tools (yt-dlp, ffmpeg) with streaming or captured output and cancellation.</summary>
public sealed class ProcessRunner
{
    /// <summary>Run a tool streaming each stdout/stderr line to callbacks. Returns the exit code.</summary>
    public async Task<int> RunStreamingAsync(
        string exePath,
        IEnumerable<string> arguments,
        string? workingDirectory,
        Action<string>? onOutput,
        Action<string>? onError,
        CancellationToken ct)
    {
        using var process = CreateProcess(exePath, arguments, workingDirectory);

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onError?.Invoke(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {exePath}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() => TryKill(process));

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return process.ExitCode;
    }

    /// <summary>Run a tool and capture all stdout/stderr. Use for short metadata queries.</summary>
    public async Task<ProcessResult> RunCaptureAsync(
        string exePath,
        IEnumerable<string> arguments,
        CancellationToken ct)
    {
        using var process = CreateProcess(exePath, arguments, null);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {exePath}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() => TryKill(process));

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static Process CreateProcess(string exePath, IEnumerable<string> arguments, string? workingDirectory)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        return new Process { StartInfo = psi, EnableRaisingEvents = true };
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Process already exited or cannot be killed; nothing else to do.
        }
    }
}
