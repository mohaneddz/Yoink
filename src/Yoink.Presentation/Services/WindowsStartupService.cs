using System.IO;
using Microsoft.Win32;

namespace Yoink.Presentation.Services;

public interface IWindowsStartupService
{
    void Apply(bool enabled, bool minimized);
}

public sealed class WindowsStartupService : IWindowsStartupService
{
    public void Apply(bool enabled, bool minimized)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        if (!enabled)
        {
            key.DeleteValue("Yoink", throwOnMissingValue: false);
            return;
        }
        var executable = Path.Combine(AppContext.BaseDirectory, "Yoink.exe");
        if (!File.Exists(executable)) throw new FileNotFoundException("The Yoink executable must be available to enable Windows startup.", executable);
        key.SetValue("Yoink", BuildCommand(executable, minimized), RegistryValueKind.String);
    }

    public static string BuildCommand(string executable, bool minimized) =>
        $"\"{executable}\"" + (minimized ? " --background" : string.Empty);
}
