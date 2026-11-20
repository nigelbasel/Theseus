using System.Runtime.CompilerServices;

namespace Theseus.Tests.Repository;

/// <summary>
/// The help covers the rebuilt project view (1.1d AC 37): its own page, linked from the help home, naming the summary,
/// the marks, the boxes, folding, the links, the pane and the keys; and Alt+Left among the shortcuts.
/// </summary>
[TestFixture]
public class ProjectViewDocsTests
{
    private static string Doc(string name) => File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", name));

    [Test]
    public void TheProjectViewPage_IsLinkedFromTheHelpHome()
    {
        Assert.That(Doc("index.md"), Does.Contain("(project-view.md)"));
        Assert.That(Doc("cards.md"), Does.Contain("(project-view.md)"));
    }

    [TestCase("## Where the project is")]
    [TestCase("## The marks")]
    [TestCase("## The boxes")]
    [TestCase("**Collapse all**")]
    [TestCase("## The links")]
    [TestCase("## The Properties pane")]
    [TestCase("## Keys")]
    [TestCase("Alt+Left")]
    [TestCase("◐ | Holdouts not run yet")]
    public void TheProjectViewPage_Covers(string part)
    {
        Assert.That(Doc("project-view.md"), Does.Contain(part));
    }

    [Test]
    public void TheProjectViewPage_NeverSaysSlugOrWorkItem()
    {
        Assert.That(Doc("project-view.md"), Does.Not.Match("(?i)slug|work item"));
    }

    [Test]
    public void TheShortcuts_IncludeAltLeft()
    {
        Assert.That(Doc("shortcuts.md"), Does.Contain("| Alt+Left |"));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
