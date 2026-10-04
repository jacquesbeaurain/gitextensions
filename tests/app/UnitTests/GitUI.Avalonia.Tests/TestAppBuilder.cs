using Avalonia;
using Avalonia.Headless;
using GitExtensionsTests;
using GitUI.Compat;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace GitExtensionsTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<GitExtensions.App>()
            .UseSkia()
            .AfterSetup(_ =>
            {
                // The real App does this in its desktop-lifetime startup path, which headless tests never take. Without it
                // the fonts and theme resources a test sees depend on which earlier test happened to apply them.
                AvaloniaFontSettings.InstallSystemDefaults();
                AvaloniaThemeSettings.ApplyAppSettings();
                AvaloniaFontSettings.ApplyAppSettings();
            })
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                // Render real frames with Skia so tests can capture and compare pixels
                // (golden-image tests); the default headless drawing produces no pixels.
                UseHeadlessDrawing = false,
            });
}
