using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Yoink.Application.Abstractions;

namespace Yoink.Presentation.Services;

/// <summary>WPF/Windows implementation of OS integration (open, reveal, clipboard, pickers).</summary>
public sealed class SystemAccess : ISystemAccess
{
    public void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        TryStart(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public void OpenFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        TryStart(new ProcessStartInfo("explorer.exe", $"\"{folderPath}\"") { UseShellExecute = true });
    }

    public void RevealInExplorer(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            // Fall back to opening the containing folder if the file is gone.
            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
            {
                OpenFolder(dir);
            }

            return;
        }

        TryStart(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
    }

    public void PlayFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        TryStart(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    public string? ReadClipboardText()
    {
        try
        {
            return System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;
        }
        catch
        {
            return null;
        }
    }

    public void SetClipboardText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text ?? string.Empty);
        }
        catch
        {
            // Clipboard can be locked by another process; ignore.
        }
    }

    public string? PickFolder(string? initialPath = null)
    {
        var dialog = new OpenFolderDialog { Multiselect = false };
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickFile(string? filter = null, string? initialPath = null)
    {
        var dialog = new OpenFileDialog { Filter = filter ?? "All files (*.*)|*.*" };
        if (!string.IsNullOrWhiteSpace(initialPath) && Directory.Exists(initialPath))
        {
            dialog.InitialDirectory = initialPath;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static void TryStart(ProcessStartInfo psi)
    {
        try
        {
            Process.Start(psi);
        }
        catch
        {
            // Nothing actionable if the shell refuses to launch.
        }
    }
}
