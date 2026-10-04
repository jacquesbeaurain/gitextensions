using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using NUnit.Framework.Interfaces;

[assembly: GitExtensionsTests.IsolateAppSettingsPerTest]

namespace GitExtensionsTests;

/// <summary>
///  Gives every test its own temporary <see cref="AppSettings"/> store, so one test's settings changes (or a
///  failed restore in a <c>finally</c> block) can never change the outcome of another test, and tests never read
///  or write the developer's real settings file. It also empties the process-wide git command cache: that cache
///  is keyed by command text only, so a repository created by one test could otherwise answer the commands of
///  another. NUnit delivers assembly-level actions to tests but not to fixtures, hence the per-test scope.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class IsolateAppSettingsPerTestAttribute : NUnitAttribute, ITestAction
{
    private static readonly Dictionary<string, TestSettings> _active = [];

    public ActionTargets Targets => ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        GitModule.GitCommandCache.Clear();
        lock (_active)
        {
            _active[test.Id] = TestSettings.Install();
        }
    }

    public void AfterTest(ITest test)
    {
        TestSettings? settings;
        lock (_active)
        {
            _active.Remove(test.Id, out settings);
        }

        settings?.Dispose();
    }

    private sealed class TestSettings : IDisposable
    {
        private readonly DistributedSettings _original;
        private readonly GitExtSettingsCache _cache;
        private readonly string _directory;

        private TestSettings(DistributedSettings original, GitExtSettingsCache cache, string directory)
        {
            _original = original;
            _cache = cache;
            _directory = directory;
        }

        public static TestSettings Install()
        {
            AppSettings.TestAccessor accessor = AppSettings.GetTestAccessor();
            string directory = Path.Combine(Path.GetTempPath(), $"GitExtensions.TestSettings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            GitExtSettingsCache cache = GitExtSettingsCache.Create(Path.Combine(directory, "GitExtensions.settings"));
            TestSettings fixtureSettings = new(accessor.SettingsContainer, cache, directory);
            accessor.SettingsContainer = new DistributedSettings(lowerPriority: null, cache, SettingLevel.Unknown);

            // CurrentTranslation is a separate static override that tests set to exercise other languages; it must
            // not leak the translated (and differently line-ended) strings into the next fixture.
            AppSettings.CurrentTranslation = null;
            return fixtureSettings;
        }

        public void Dispose()
        {
            AppSettings.GetTestAccessor().SettingsContainer = _original;
            AppSettings.CurrentTranslation = null;
            _cache.Dispose();
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // A late write by a background task must not fail an unrelated test run.
            }
        }
    }
}
