using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Yoink.Presentation.Services;

/// <summary>
/// Manages application-wide theme switching (Light, Dark, System) by updating WPF resource brushes.
/// </summary>
public static class ThemeManager
{
    public static string CurrentTheme { get; private set; } = "Light";

    public static void ApplyTheme(string themeName)
    {
        CurrentTheme = string.IsNullOrWhiteSpace(themeName) ? "Light" : themeName;
        var isDark = CurrentTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase);

        if (CurrentTheme.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            isDark = IsSystemDarkMode();
        }

        var app = System.Windows.Application.Current;
        if (app is null) return;

        var res = app.Resources;

        if (isDark)
        {
            SetColorAndBrush(res, "AppShellColor", "AppShellBrush", "#FF0D0D0E");
            SetColorAndBrush(res, "SidebarColor", "SidebarBrush", "#FF0A0A0B");
            SetColorAndBrush(res, "SurfacePrimaryColor", "SurfacePrimaryBrush", "#FF141416");
            SetColorAndBrush(res, "SurfaceSecondaryColor", "SurfaceSecondaryBrush", "#FF222228");
            SetColorAndBrush(res, "CardBackgroundColor", "CardBackgroundBrush", "#FF1F1F24");
            SetColorAndBrush(res, "WhiteColor", "WhiteBrush", "#FF1F1F24");
            SetColorAndBrush(res, "InputBackgroundColor", "InputBackgroundBrush", "#FF19191D");
            SetColorAndBrush(res, "SecondaryButtonBackgroundColor", "SecondaryButtonBackgroundBrush", "#FF26262C");
            SetColorAndBrush(res, "TextPrimaryColor", "TextPrimaryBrush", "#FFF0F0F5");
            SetColorAndBrush(res, "TextMutedColor", "TextMutedBrush", "#FFA0A0A8");
            SetColorAndBrush(res, "DividerColor", "DividerBrush", "#FF2E2E36");
            SetColorAndBrush(res, "DisabledColor", "DisabledBrush", "#FF484852");
            SetColorAndBrush(res, "SidebarTextInactiveColor", "SidebarTextInactiveBrush", "#FFA0A0A6");
            SetColorAndBrush(res, "SidebarTextMutedColor", "SidebarTextMutedBrush", "#FF707078");
            SetBrush(res, "SidebarHoverBrush", "#FF1E1E24");
            SetBrush(res, "SidebarActiveBackgroundBrush", "#FF2B1D16");
            SetColorAndBrush(res, "RowHoverColor", "RowHoverBrush", "#FF26262E");
            SetColorAndBrush(res, "RowSelectedColor", "RowSelectedBrush", "#FF36241C");
            SetColorAndBrush(res, "LogPanelColor", "LogPanelBrush", "#FF0A0A0E");
        }
        else
        {
            SetColorAndBrush(res, "AppShellColor", "AppShellBrush", "#FF090909");
            SetColorAndBrush(res, "SidebarColor", "SidebarBrush", "#FF050505");
            SetColorAndBrush(res, "SurfacePrimaryColor", "SurfacePrimaryBrush", "#FFF7F7F4");
            SetColorAndBrush(res, "SurfaceSecondaryColor", "SurfaceSecondaryBrush", "#FFEFEFED");
            SetColorAndBrush(res, "CardBackgroundColor", "CardBackgroundBrush", "#FFFFFFFF");
            SetColorAndBrush(res, "WhiteColor", "WhiteBrush", "#FFFFFFFF");
            SetColorAndBrush(res, "InputBackgroundColor", "InputBackgroundBrush", "#FFFFFFFF");
            SetColorAndBrush(res, "SecondaryButtonBackgroundColor", "SecondaryButtonBackgroundBrush", "#FFFFFFFF");
            SetColorAndBrush(res, "TextPrimaryColor", "TextPrimaryBrush", "#FF111111");
            SetColorAndBrush(res, "TextMutedColor", "TextMutedBrush", "#FF6D6D68");
            SetColorAndBrush(res, "DividerColor", "DividerBrush", "#FFCFCFC9");
            SetColorAndBrush(res, "DisabledColor", "DisabledBrush", "#FFA7A7A1");
            SetColorAndBrush(res, "SidebarTextInactiveColor", "SidebarTextInactiveBrush", "#FFC9C9C4");
            SetColorAndBrush(res, "SidebarTextMutedColor", "SidebarTextMutedBrush", "#FF7A7A76");
            SetBrush(res, "SidebarHoverBrush", "#FF141414");
            SetBrush(res, "SidebarActiveBackgroundBrush", "#FF1A1310");
            SetColorAndBrush(res, "RowHoverColor", "RowHoverBrush", "#FFF0F0EC");
            SetColorAndBrush(res, "RowSelectedColor", "RowSelectedBrush", "#FFFCEEE7");
            SetColorAndBrush(res, "LogPanelColor", "LogPanelBrush", "#FF0C0C0C");
        }
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i)
            {
                return i == 0;
            }
        }
        catch { }

        return false;
    }

    private static void SetColorAndBrush(ResourceDictionary res, string colorKey, string brushKey, string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            res[colorKey] = color;
            res[brushKey] = new SolidColorBrush(color);
        }
        catch { }
    }

    private static void SetBrush(ResourceDictionary res, string brushKey, string hex)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            res[brushKey] = new SolidColorBrush(color);
        }
        catch { }
    }
}
