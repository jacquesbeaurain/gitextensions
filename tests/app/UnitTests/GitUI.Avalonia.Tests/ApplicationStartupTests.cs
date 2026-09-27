using GitCommands;
using GitExtensions;

namespace GitExtensionsTests;

[TestFixture]
[NonParallelizable]
public sealed class ApplicationStartupTests
{
    [TestCase(true, "wayland-0", true)]
    [TestCase(true, null, false)]
    [TestCase(true, "", false)]
    [TestCase(false, "wayland-0", false)]
    [Category("P0_6")]
    public void ShouldUseWayland_should_require_a_linux_wayland_session(
        bool isLinux,
        string? waylandDisplay,
        bool expected)
    {
        Program.ShouldUseWayland(isLinux, waylandDisplay).Should().Be(expected);
    }

    [TestCase("GitExtensions.exe")]
    [TestCase("GitExtensions")]
    [TestCase("GitExtensions.Avalonia.exe")]
    [TestCase("GitExtensions.Avalonia")]
    [TestCase("dotnet.exe")]
    [TestCase("dotnet")]
    [TestCase("testhost.exe")]
    [TestCase("testhost")]
    public void GetGitExtensionsFullPath_should_accept_expected_entry_points(string executableFileName)
    {
        AppSettings.TestAccessor accessor = AppSettings.GetTestAccessor();
        string originalPath = accessor.ApplicationExecutablePath;
        string candidatePath = Path.Combine(TestContext.CurrentContext.WorkDirectory, executableFileName);

        try
        {
            accessor.ApplicationExecutablePath = candidatePath;

            AppSettings.GetGitExtensionsFullPath().Should().Be(candidatePath);
        }
        finally
        {
            accessor.ApplicationExecutablePath = originalPath;
        }
    }
}
