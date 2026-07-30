namespace StreamVault.Application.Abstractions;

/// <summary>OS-level integration: opening files/folders/URLs, clipboard, and folder/file pickers.</summary>
public interface ISystemAccess
{
    void OpenUrl(string url);
    void OpenFolder(string folderPath);
    void RevealInExplorer(string filePath);
    void PlayFile(string filePath);
    string? ReadClipboardText();
    void SetClipboardText(string text);

    /// <summary>Show a folder picker; returns null if cancelled.</summary>
    string? PickFolder(string? initialPath = null);

    /// <summary>Show a file picker; returns null if cancelled.</summary>
    string? PickFile(string? filter = null, string? initialPath = null);
}
