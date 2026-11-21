using System.Runtime.CompilerServices;

namespace Theseus.Tests.Repository;

/// <summary>
/// The help covers the project card (1.1d AC 38 to 44): what marks it as a project, where it is, its colour and Project
/// properties, selecting and dragging it, starting and stopping the whole project, and a dimmed card; and Enter opens it.
/// </summary>
[TestFixture]
public class ProjectCardDocsTests
{
    private static string Doc(string name) => File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", name));

    [TestCase("**PROJECT**")]
    [TestCase("stacked-card mark")]
    [TestCase("leaving out any that are zero")]
    [TestCase("**Project properties…**")]
    [TestCase("View > Columns by > Stage")]
    [TestCase("the project was skipped")]
    [TestCase("**⏸ Project stopped**")]
    [TestCase("No card of Theseus bootstrap can start: git-22 is already running")]
    [TestCase("keeping its mark, colour and the word PROJECT")]
    public void TheCardsPage_CoversTheProjectCard(string part)
    {
        Assert.That(Doc("cards.md"), Does.Contain(part));
    }

    [Test]
    public void TheProjectViewPage_SaysTheTabsAndSummaryTakeTheProjectsColour_AndTheCardShowsTheSameSentence()
    {
        Assert.That(Doc("project-view.md"), Does.Contain("in the project's own colour").And.Contain("The project card shows the same"));
    }

    [Test]
    public void TheShortcuts_SayEnterOpensAProjectCard()
    {
        Assert.That(Doc("shortcuts.md"), Does.Contain("| Enter | On a project card"));
    }

    [Test]
    public void TheCardsPage_NeverSaysSlugOrWorkItem()
    {
        Assert.That(Doc("cards.md"), Does.Not.Match("(?i)slug|work item"));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
