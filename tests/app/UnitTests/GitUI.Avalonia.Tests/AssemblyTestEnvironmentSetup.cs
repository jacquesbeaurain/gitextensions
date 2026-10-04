using GitUI;
using Microsoft.VisualStudio.Threading;

/// <summary>
///  Assembly-wide process state that the application sets up once at startup. It lives in the global namespace so
///  it applies to every test, and tests never depend on another fixture having initialized it first.
/// </summary>
[SetUpFixture]
public sealed class AssemblyTestEnvironmentSetup
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Many fixtures call ThreadHelper without installing a context themselves; the namespace-scoped
        // SharedGitUITestsSetup only reaches the tests in its own namespace.
        ThreadHelper.JoinableTaskContext = new JoinableTaskContext();
        UserEnvironmentInformation.Initialise("9999999999999999999999999999999999abcdef", isDirty: true);
    }
}
