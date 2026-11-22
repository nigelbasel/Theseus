using System.Runtime.CompilerServices;

namespace Theseus.Tests.Repository;

/// <summary>The help covers 1.1e's dragging, ↗ Open and Add to project… from the view (AC 1 to 6).</summary>
[TestFixture]
public class ProjectsInUseDocsTests
{
    private static string Doc(string name) => File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", name));

    [TestCase("board.md", "only along what you place by hand")]
    [TestCase("board.md", "cards move up and down between areas only")]
    [TestCase("cards.md", "in the column of its most urgent running card")]
    [TestCase("cards.md", "in the row of its first card")]
    [TestCase("cards.md", "**↗ Open**, the first button on a project card")]
    [TestCase("project-view.md", "offers **Add to project…**, which opens the board's window with this project")]
    [TestCase("cards.md", "those cards again, with no list to tick")]
    [TestCase("cards.md", "decides what runs. Stop it anyway?")]
    [TestCase("cards.md", "becoming **Restart for more work**")]
    [TestCase("cards.md", "**Stopped by you**")]
    [TestCase("cards.md", "OK writes a new title")]
    [TestCase("holdouts.md", "marked **(default)**")]
    [TestCase("holdouts.md", "**Write scenarios…** beside it creates a card")]
    [TestCase("project-view.md", "a brace to the left of the line groups them all")]
    [TestCase("project-view.md", "A button that cannot act now is shown disabled")]
    [TestCase("cards.md", "is kept by Theseus, never in `project.md`. It covers the primary branch")]
    [TestCase("cards.md", "shows **project branch rules**")]
    [TestCase("holdouts.md", "installing this version's guard then would protect only the default folder")]
    [TestCase("chains.md", "A session acts only on its **descendants**")]
    [TestCase("chains.md", "`start_workstream`")]
    [TestCase("cards.md", "says only **Needs updating**")]
    [TestCase("project-view.md", "## The spec window")]
    [TestCase("chains.md", "`events` (runs without asking)")]
    [TestCase("chains.md", "`gates` (runs without asking)")]
    [TestCase("project-view.md", "You accepted this in <card>'s tab: Confirm…")]
    [TestCase("project-view.md", "## Writing prompts")]
    [TestCase("cards.md", "**Not a project** on its menu hides a folder's card")]
    public void TheHelp_SaysHowProjectsAreUsed(string page, string part)
    {
        Assert.That(Doc(page), Does.Contain(part));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
