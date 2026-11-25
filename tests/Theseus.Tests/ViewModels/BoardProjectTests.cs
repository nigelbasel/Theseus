using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The board with projects (project-view AC 12, 13, 15, 22): a readable project's cards leave the grid and its project
/// card stands for them, in the cell it was dragged to; an unreadable project's cards come back; with no projects the
/// grid is exactly as before.
/// </summary>
[TestFixture]
public class BoardProjectTests
{
    private static BoardFixture Board()
    {
        // "member" and "other-member" are the cards a-project's file names; the rule below looks them up by key (1.1c).
        var fixture = new BoardFixture().With("ad-hoc").With("member").With("other-member", column: "In progress");
        fixture.Board.Refresh();

        return fixture;
    }

    private static IReadOnlyList<string> InTheGrid(BoardFixture fixture) =>
        [.. fixture.Board.Cells.SelectMany(cell => cell.Cards).Select(card => card.Key).Order()];

    private static void ReadableProject(BoardFixture fixture) =>
        fixture.Board.KeepOutOfGrid = card => card.Key is "member" or "other-member";

    [Test]
    public void WithNoProjects_EveryCardIsInTheGrid_AndNoCellHoldsAProjectCard()
    {
        var fixture = Board();
        fixture.Board.ShowProjects([]);

        Assert.That(InTheGrid(fixture), Is.EqualTo(new[] { "ad-hoc", "member", "other-member" }));
        Assert.That(fixture.Board.Cells.SelectMany(cell => cell.Projects), Is.Empty, "nothing on screen changes (AC 22)");
    }

    [Test]
    public void AReadableProjectsCards_LeaveTheGrid_ButAreStillTheBoardsCards()
    {
        var fixture = Board();
        ReadableProject(fixture);
        fixture.Board.ShowProjects([new ProjectCardViewModel("a-project", "A project")]);

        Assert.That(InTheGrid(fixture), Is.EqualTo(new[] { "ad-hoc" }));
        Assert.That(fixture.Board.Cards.Select(card => card.Key).Order(), Is.EqualTo(new[] { "ad-hoc", "member", "other-member" }),
            "starting, stopping and the selection still reach them");
    }

    [Test]
    public void WhenTheProjectCanNoLongerBeRead_ItsCardsComeBackToTheGrid()
    {
        var fixture = Board();
        ReadableProject(fixture);
        fixture.Board.ShowProjects([new ProjectCardViewModel("a-project", "A project")]);

        fixture.Board.KeepOutOfGrid = null;
        fixture.Board.ShowProjects([]);

        Assert.That(InTheGrid(fixture), Is.EqualTo(new[] { "ad-hoc", "member", "other-member" }));
        Assert.That(fixture.Board.Cells.SelectMany(cell => cell.Projects), Is.Empty);
    }

    [Test]
    public void AProjectCard_SitsInTheFirstCellUntilDragged_ThenWhereItWasDropped_AcrossARestart()
    {
        var fixture = Board();
        var project = new ProjectCardViewModel("a-project", "A project");
        fixture.Board.ShowProjects([project]);

        Assert.That(fixture.Board.Cells.First().Projects, Is.EqualTo(new[] { project }));

        fixture.Board.MoveProject("a-project", "In progress", "Dev");

        var cell = fixture.Board.Cells.Single(each => each.Projects.Contains(project));
        Assert.That((cell.Column, cell.Area), Is.EqualTo(("In progress", "Dev")));

        var reopened = fixture.Reopen();
        reopened.ShowProjects([project]);

        var again = reopened.Cells.Single(each => each.Projects.Contains(project));
        Assert.That((again.Column, again.Area), Is.EqualTo(("In progress", "Dev")));
    }

    [Test]
    public void WhileTheProjectViewIsOpen_ShiftSelectsARunInTheViewsOrder()
    {
        var fixture = Board();
        ReadableProject(fixture);
        var member = fixture.Card("member")!;
        var other = fixture.Card("other-member")!;
        var adHoc = fixture.Card("ad-hoc")!;
        fixture.Board.SelectionOrder = () => [other, member];

        fixture.Board.ClickSelect(other, toggle: false, extend: false);
        fixture.Board.ClickSelect(member, toggle: false, extend: true);

        Assert.That((member.IsSelected, other.IsSelected, adHoc.IsSelected), Is.EqualTo((true, true, false)));
    }
}
