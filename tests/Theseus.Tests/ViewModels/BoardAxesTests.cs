using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Dragging along the axes the owner places by hand, and only those (1.1e AC 1 to 4, 7; Nigel, 2026-11-21): stages
/// between columns, areas between rows, for workstream and project cards alike. A derived axis is never stored, and a
/// project on one sits by its cards.
/// </summary>
[TestFixture]
public class BoardAxesTests
{
    private static ColumnViewModel Cell(BoardFixture fixture, string column, string row) =>
        fixture.Board.Cells.Single(cell => cell.Column == column && cell.Area == row);

    private static ColumnViewModel CellOf(BoardFixture fixture, ProjectCardViewModel project) =>
        fixture.Board.Cells.Single(cell => cell.Projects.Contains(project));

    [Test]
    public void EachAxis_CanBeDraggedAlong_OnlyWhilePlacedByHand()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        Assert.That((fixture.Board.CanMoveColumns, fixture.Board.CanMoveRows, fixture.Board.CanDrag), Is.EqualTo((true, true, true)));

        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;
        Assert.That((fixture.Board.CanMoveColumns, fixture.Board.CanMoveRows, fixture.Board.CanDrag), Is.EqualTo((false, true, true)),
            "session-state columns and area rows: up and down only");

        fixture.Board.RowsBy = BoardRowsBy.Chain;
        Assert.That(fixture.Board.CanDrag, Is.False, "neither axis is placed by hand");

        fixture.Board.ColumnsBy = BoardColumnsBy.Stage;
        Assert.That((fixture.Board.CanMoveColumns, fixture.Board.CanMoveRows), Is.EqualTo((true, false)));
    }

    [Test]
    public void UnderSessionStateColumns_ACardMovesBetweenAreas_AndKeepsItsStage()
    {
        var fixture = new BoardFixture().With("one", "In progress", "Research");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        var notice = fixture.Board.Move("one", BoardView.NotRunning, "Dev");

        Assert.That(notice, Is.Null);
        Assert.That((fixture.Card("one")!.Column, fixture.Card("one")!.Area), Is.EqualTo(("In progress", "Dev")));
        Assert.That(Cell(fixture, BoardView.NotRunning, "Dev").Cards.Select(card => card.Key), Is.EqualTo(new[] { "one" }));

        fixture.Board.ColumnsBy = BoardColumnsBy.Stage;

        Assert.That(Cell(fixture, "In progress", "Dev").Cards.Select(card => card.Key), Is.EqualTo(new[] { "one" }),
            "switching back shows it where it was put");
    }

    [Test]
    public void ADropAcrossADerivedColumn_KeepsTheStage_MovesTheArea_AndSaysWhyOnce()
    {
        var fixture = new BoardFixture().With("one", "In progress", "Research");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        var notice = fixture.Board.Move("one", BoardView.Working, "Dev");

        Assert.That(notice, Is.EqualTo("Columns show session state: a card's place there follows its work"));
        Assert.That((fixture.Card("one")!.Column, fixture.Card("one")!.Area), Is.EqualTo(("In progress", "Dev")));
        Assert.That(Cell(fixture, BoardView.NotRunning, "Dev").Cards.Select(card => card.Key), Is.EqualTo(new[] { "one" }));
    }

    [Test]
    public void UnderFolderRows_ADropInAnotherFolder_MovesTheStageOnly_AndSaysRowsShowFolders()
    {
        var fixture = new BoardFixture().With("one", "New", "Research", @"<root>\Theseus").With("two", folder: @"<root>\Other");
        fixture.Board.Refresh();
        fixture.Board.RowsBy = BoardRowsBy.Project;

        var notice = fixture.Board.Move("one", "In progress", "Other");

        Assert.That(notice, Is.EqualTo("Rows show folders: a card's place there follows its work"));
        Assert.That((fixture.Card("one")!.Column, fixture.Card("one")!.Area), Is.EqualTo(("In progress", "Research")),
            "a folder's name is never stored as its area");
        Assert.That(Cell(fixture, "In progress", "Theseus").Cards.Select(card => card.Key), Is.EqualTo(new[] { "one" }));
    }

    [Test]
    public void ADropOnlyAlongADerivedAxis_ChangesNothing_ButSaysWhy()
    {
        var fixture = new BoardFixture().With("one", "New", "Research", @"<root>\Theseus").With("two", folder: @"<root>\Other");
        fixture.Board.Refresh();
        fixture.Board.RowsBy = BoardRowsBy.Chain;
        fixture.Board.ChainOf = key => key == "one" ? "Chain A" : null;
        fixture.Board.Regroup();

        var notice = fixture.Board.Move("one", "New", BoardView.NoChain);

        Assert.That(notice, Is.EqualTo("Rows show chains: a card's place there follows its work"));
        Assert.That((fixture.Card("one")!.Column, fixture.Card("one")!.Area), Is.EqualTo(("New", "Research")));
        Assert.That(Cell(fixture, "New", "Chain A").Cards.Select(card => card.Key), Is.EqualTo(new[] { "one" }));
    }

    [Test]
    public void WithNeitherAxisPlacedByHand_ADropDoesNothing()
    {
        var fixture = new BoardFixture().With("one", "New", "Research", @"<root>\Theseus");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;
        fixture.Board.RowsBy = BoardRowsBy.Project;

        Assert.That(fixture.Board.Move("one", BoardView.Working, "Theseus"), Is.Null);
        Assert.That(fixture.Card("one")!.Column, Is.EqualTo("New"));
        Assert.That(BoardGesture.Refusal(fixture.Board), Is.Not.Null);
    }

    [Test]
    public void AProjectCard_KeepsBothPlaces_AcrossASwitchOfAxes()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P") { StateColumn = BoardView.Idle };
        fixture.Board.ShowProjects([project]);

        fixture.Board.MoveProject("p", "In progress", "Dev");
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        Assert.That((CellOf(fixture, project).Column, CellOf(fixture, project).Area), Is.EqualTo((BoardView.Idle, "Dev")),
            "its column follows its cards, its row is the one it was put in");

        Assert.That(fixture.Board.MoveProject("p", BoardView.NeedsYou, "Research"),
            Is.EqualTo("Columns show session state: a card's place there follows its work"));
        Assert.That((CellOf(fixture, project).Column, CellOf(fixture, project).Area), Is.EqualTo((BoardView.Idle, "Research")));

        fixture.Board.ColumnsBy = BoardColumnsBy.Stage;

        Assert.That((CellOf(fixture, project).Column, CellOf(fixture, project).Area), Is.EqualTo(("In progress", "Research")));
        Assert.That(fixture.Settings.ProjectCardPlaces["p"], Is.EqualTo(new ProjectCardPlace("In progress", "Research")));
    }

    [Test]
    public void AProjectDroppedUnderFolderRows_StoresItsStage_ButNeverTheFoldersName()
    {
        var fixture = new BoardFixture().With("member", folder: @"<root>\Theseus").With("other", folder: @"<root>\Other");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P") { FirstCardKey = "member" };
        fixture.Board.ShowProjects([project]);
        fixture.Board.MoveProject("p", "New", "Dev");
        fixture.Board.RowsBy = BoardRowsBy.Project;

        var notice = fixture.Board.MoveProject("p", "In progress", "Other");

        Assert.That(notice, Is.EqualTo("Rows show folders: a card's place there follows its work"));
        Assert.That(fixture.Settings.ProjectCardPlaces["p"], Is.EqualTo(new ProjectCardPlace("In progress", "Dev")));
        Assert.That((CellOf(fixture, project).Column, CellOf(fixture, project).Area), Is.EqualTo(("In progress", "Theseus")),
            "in the row of its first card");
    }

    [Test]
    public void AProjectFirstPlacedUnderSessionStateColumns_KeepsItsAreaUnderStages()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P");
        fixture.Board.ShowProjects([project]);
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        fixture.Board.MoveProject("p", BoardView.NotRunning, "Dev");
        fixture.Board.ColumnsBy = BoardColumnsBy.Stage;

        Assert.That((CellOf(fixture, project).Column, CellOf(fixture, project).Area), Is.EqualTo(("New", "Dev")));
    }

    [Test]
    public void UnderSessionStateColumns_AProjectSitsInItsCardsColumn()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P") { StateColumn = BoardView.NeedsYou };
        fixture.Board.ShowProjects([project]);
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        Assert.That(CellOf(fixture, project).Column, Is.EqualTo(BoardView.NeedsYou));
    }

    [Test]
    public void UnderStageColumns_ADropAboveAnotherCard_RewritesBothPlansOrder()
    {
        var fixture = new BoardFixture().With("a").With("b");
        fixture.Board.Refresh();
        fixture.Board.Move("a", "New", "Research", 0);
        fixture.Board.Move("b", "New", "Research", 0);

        Assert.That(Cell(fixture, "New", "Research").Cards.Select(card => card.Key), Is.EqualTo(new[] { "b", "a" }));
        Assert.That((fixture.Plans.TryRead("b")!.Order, fixture.Plans.TryRead("a")!.Order), Is.EqualTo((1, 2)));
    }

    [Test]
    public void UnderSessionStateColumns_AMoveBetweenAreas_WritesNoPlansOrder_AndKeepsTheStage()
    {
        var fixture = new BoardFixture().With("a", "In progress").With("b", "In progress", "Dev");
        fixture.Board.Refresh();
        var before = (fixture.Plans.TryRead("a")!.Order, fixture.Plans.TryRead("b")!.Order);
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        fixture.Board.Move("a", BoardView.NotRunning, "Dev", 0);

        Assert.That((fixture.Plans.TryRead("a")!.Order, fixture.Plans.TryRead("b")!.Order), Is.EqualTo(before));
        Assert.That((fixture.Card("a")!.Column, fixture.Card("a")!.Area), Is.EqualTo(("In progress", "Dev")));
    }

    [Test]
    public void UnderChainRows_AProjectSitsInItsFirstCardsChain()
    {
        var fixture = new BoardFixture().With("member").With("other");
        fixture.Board.Refresh();
        fixture.Board.ChainOf = key => key == "member" ? "Chain A" : null;
        var project = new ProjectCardViewModel("p", "P") { FirstCardKey = "member" };
        fixture.Board.ShowProjects([project]);
        fixture.Board.RowsBy = BoardRowsBy.Chain;

        Assert.That(CellOf(fixture, project).Area, Is.EqualTo("Chain A"));
    }
}
