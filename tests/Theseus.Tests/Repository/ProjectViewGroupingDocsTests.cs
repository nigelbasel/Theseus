using System.Runtime.CompilerServices;

namespace Theseus.Tests.Repository;

/// <summary>
/// The help covers PR 4 (1.1d AC 45 to 54): Group by and what the view grouped by objectives shows, and Properties in a
/// narrow window, with Esc closing it first among the shortcuts.
/// </summary>
[TestFixture]
public class ProjectViewGroupingDocsTests
{
    private static string Doc(string name) => File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", name));

    [TestCase("## Group by")]
    [TestCase("**Group by: Flow | Objectives**")]
    [TestCase("**View > Group project by**")]
    [TestCase("**nothing yet**")]
    [TestCase("**Not in project.md**")]
    [TestCase("**← handed on from …**")]
    [TestCase("**Add objective…**")]
    [TestCase("**Edit\nobjective…**")]
    [TestCase("**Properties\nhidden: the window is too narrow**")]
    [TestCase("**×** or Esc")]
    public void TheProjectViewPage_Covers(string part)
    {
        Assert.That(Doc("project-view.md").Replace("\r\n", "\n", StringComparison.Ordinal), Does.Contain(part));
    }

    [Test]
    public void TheShortcuts_SayEscClosesPropertiesFirst_InAProjectsView()
    {
        Assert.That(Doc("shortcuts.md"), Does.Contain("| Esc | Clear the selection; in a project's view, first closes Properties"));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
