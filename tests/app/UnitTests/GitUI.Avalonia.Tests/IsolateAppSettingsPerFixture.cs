using GitCommands;
using GitCommands.Git;
using GitCommands.Settings;
using GitExtensions.Extensibility.Settings;
using NUnit.Framework.Interfaces;

[assembly: GitExtensionsTests.IsolateAppSettingsPerFixture]

namespace GitExtensionsTests;

/// <summary>
///  Gives every test fixture its own temporary <see cref="AppSettings"/> store, so one fixture's settings
///  changes (or a failed restore in a <c>finally</c> block) can never change the outcome of another fixture,
///  and tests never read or write the developer's real settings file. It also empties the process-wide git
///  command cache before every test: that cache is keyed by command text only, so a repository created by one
///  test could otherwise answer the commands of another.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class IsolateAppSettingsPerFixtureAttribute : NUnitAttribute, ITestAction
{
    private static readonly Dictionary<string, FixtureSettings> _active = [];

    public ActionTargets Targets => ActionTargets.Suite | ActionTargets.Test;

    public void BeforeTest(ITest test)
    {
        GitModule.GitCommandCache.Clear();
        if (!IsFixture(test))
        {
            return;
        }

        lock (_active)
        {
            _active[test.FullName] = FixtureSettings.Install();
        }
    }

    public void AfterTest(ITest test)
    {
        if (!IsFixture(test))
        {
            return;
        }

        FixtureSettings? settings;
        lock (_active)
        {
            _active.Remove(test.FullName, out settings);
        }

        settings?.Dispose();
    }

    // Namespace and assembly suites also receive suite actions; only a class fixture owns its own settings.
    private static bool IsFixture(ITest test) => test.IsSuite && test.TypeInfo is not null;

    private sealed class FixtureSettings : IDisposable
    {
        private readonly DistributedSettings _original;
        private readonly GitExtSettingsCache _cache;
        private readonly string _directory;

        private FixtureSettings(DistributedSettings original, GitExtSettingsCache cache, string directory)
        {
            _original = original;
            _cache = cache;
            _directory = directory;
        }

        public static FixtureSettings Install()
        {
            AppSettings.TestAccessor accessor = AppSettings.GetTestAccessor();
            string directory = Path.Combine(Path.GetTempPath(), $"GitExtensions.TestSettings-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);
            GitExtSettingsCache cache = GitExtSettingsCache.Create(Path.Combine(directory, "GitExtensions.settings"));
            FixtureSettings fixtureSettings = new(accessor.SettingsContainer, cache, directory);
            accessor.SettingsContainer = new DistributedSettings(lowerPriority: null, cache, SettingLevel.Unknown);
            return fixtureSettings;
        }

        public void Dispose()
        {
            AppSettings.GetTestAccessor().SettingsContainer = _original;
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
