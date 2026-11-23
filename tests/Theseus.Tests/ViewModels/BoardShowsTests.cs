using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The board shows all cards, only the project cards, or only the workstreams (Nigel, 2026-11-23: "a button to show only
/// projects, or show only workstreams or show all"), and remembers which.
/// </summary>
[TestFixture]
public class BoardShowsTests
{
    private static (BoardFixture Fixture, ProjectCardViewModel Project) Board()
    {
        var fixture = new BoardFixture().With("ad-hoc").With("other");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("bym-mcp", "BuildYourMarket MCP");
        fixture.Board.ShowProjects([project]);

        return (fixture, project);
    }

    private static bool OnBoard(BoardFixture fixture, ProjectCardViewModel project) =>
        fixture.Board.Cells.Any(cell => cell.Projects.Contains(project));

    [Test]
    public void ByDefault_TheBoardShowsProjectsAndWorkstreams()
    {
        var (fixture, project) = Board();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.ShowsAll, Is.True);
            Assert.That(fixture.Board.GridCards, Has.Count.EqualTo(2));
            Assert.That(OnBoard(fixture, project), Is.True);
        });
    }

    [Test]
    public void ShowingProjects_LeavesOnlyTheProjectCards()
    {
        var (fixture, project) = Board();

        fixture.Board.ShowsProjects = true;

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.GridCards, Is.Empty);
            Assert.That(OnBoard(fixture, project), Is.True);
        });
    }

    [Test]
    public void ShowingWorkstreams_LeavesOnlyTheWorkstreams()
    {
        var (fixture, project) = Board();

        fixture.Board.ShowsWorkstreams = true;

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.GridCards, Has.Count.EqualTo(2));
            Assert.That(OnBoard(fixture, project), Is.False);
        });
    }

    [Test]
    public void TheChoice_IsRemembered()
    {
        var (fixture, _) = Board();

        fixture.Board.ShowsProjects = true;

        Assert.That(fixture.Settings.BoardShows, Is.EqualTo(BoardShows.Projects));
    }
}
