using Yoink.Domain.Settings;

namespace Yoink.Tests;

public class SettingsWindowGeometryTests
{
    [Fact]
    public void AppSettings_has_valid_window_geometry_defaults()
    {
        var settings = new AppSettings();

        Assert.Equal(1440, settings.WindowWidth);
        Assert.Equal(960, settings.WindowHeight);
        Assert.Null(settings.WindowLeft);
        Assert.Null(settings.WindowTop);
        Assert.False(settings.IsWindowMaximized);
    }

    [Fact]
    public void AppSettings_updates_window_geometry()
    {
        var settings = new AppSettings
        {
            WindowWidth = 1400,
            WindowHeight = 900,
            WindowLeft = 100,
            WindowTop = 50,
            IsWindowMaximized = true
        };

        Assert.Equal(1400, settings.WindowWidth);
        Assert.Equal(900, settings.WindowHeight);
        Assert.Equal(100, settings.WindowLeft);
        Assert.Equal(50, settings.WindowTop);
        Assert.True(settings.IsWindowMaximized);
    }
}
