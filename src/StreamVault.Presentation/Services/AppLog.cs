using System.IO;
using System.Text;

namespace StreamVault.Presentation.Services;

/// <summary>
/// Minimal, dependency-free file logger writing to the app data <c>logs</c> folder.
/// Backs the Settings "View Logs" action and captures unhandled exceptions so a
/// deployed build never dies silently.
/// </summary>
public static class AppLog
{
    private static readonly object Gate = new();

    /// <summary>Root data directory shared with the rest of the app (settings, database, tools).</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StreamVault");

    /// <summary>Directory that the Settings "View Logs" button opens.</summary>
    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "logs");

    private static string LogFile => Path.Combine(LogDirectory, $"streamvault-{DateTime.Now:yyyy-MM-dd}.log");

    /// <summary>When false, only warnings and errors are written (mirrors the Diagnostic logging setting).</summary>
    public static bool Verbose { get; set; }

    public static void Info(string message)
    {
        if (Verbose)
        {
            Write("INFO", message);
        }
    }

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
        => Write("ERROR", ex is null ? message : $"{message}{Environment.NewLine}{ex}");

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(LogFile, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never throw and take the app down with it.
        }
    }
}
