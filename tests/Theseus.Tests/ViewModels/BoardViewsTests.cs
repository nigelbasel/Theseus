using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// View > Columns by and Rows by (Option E, Nigel 2026-10-30): the same cards, grouped by stage or by
/// what their sessions are doing, and by area, project, chain or not at all.
/// </summary>
[TestFixture]
public class BoardViewsTests
{
    [Test]
    public void RowsByProject_GroupsCardsByTheirFolder()
    {
        var fixture = new BoardFixture()
            .With("theseus-ui", folder: @"C:\dev\git\Theseus")
            .With("theseus-docs", "In progress", "Dev", @"C:\dev\git\Theseus")
            .With("tailhawk", folder: @"C:\dev\git\TailHawk");
        fixture.Board.Refresh();

        fixture.Board.RowsBy = BoardRowsBy.Project;

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Rows.Select(row => row.Area), Is.EqualTo(new[] { "TailHawk", "Theseus" }));
            Assert.That(Row(fixture, "Theseus").Count, Is.EqualTo(2));
            Assert.That(fixture.Settings.RowsBy, Is.EqualTo(BoardRowsBy.Project), "remembered");
            Assert.That(fixture.Board.Areas, Is.EqualTo(new[] { "Research", "Dev" }), "the configured areas are untouched");
        });
    }

    [Test]
    public void ColumnsBySessionState_FollowsTheSessionsOnRegroup()
    {
        var fixture = new BoardFixture().With("busy").With("quiet").With("finished", "Done");
        fixture.Board.Refresh();
        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;

        fixture.Card("busy")!.Lifecycle = SessionLifecycle.Running;
        fixture.Card("quiet")!.Lifecycle = SessionLifecycle.NeedsAttention;
        fixture.Board.Regroup();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.StageHeaders.Select(header => header.Stage), Is.EqualTo(BoardView.StateColumns));
            Assert.That(ColumnOf(fixture, "busy"), Is.EqualTo(BoardView.Working));
            Assert.That(ColumnOf(fixture, "quiet"), Is.EqualTo(BoardView.NeedsYou));
            Assert.That(ColumnOf(fixture, "finished"), Is.EqualTo(BoardView.Done));
            Assert.That(fixture.Card("busy")!.Column, Is.EqualTo("New"), "the stage itself is not changed");
        });
    }

    [Test]
    public void RowsByChain_NamesEachChainAfterItsFirstCard()
    {
        var fixture = new BoardFixture().With("analysis").With("design").With("loner");
        fixture.Board.ChainOf = key => key is "analysis" or "design" ? "analysis" : null;
        fixture.Board.Refresh();

        fixture.Board.RowsBy = BoardRowsBy.Chain;

        Assert.That(fixture.Board.Rows.Select(row => row.Area), Is.EqualTo(new[] { "analysis", BoardView.NoChain }),
            "chains first, then the cards in none");
    }

    [Test]
    public void RowsByNone_PutsEverythingInOneRow()
    {
        var fixture = new BoardFixture().With("one", area: "Research").With("two", area: "Dev");
        fixture.Board.Refresh();

        fixture.Board.RowsBy = BoardRowsBy.None;

        Assert.That(fixture.Board.Rows.Single().Count, Is.EqualTo(2));
    }

    [Test]
    public void Folds_AreKeptPerViewAndComeBackWithIt()
    {
        var fixture = new BoardFixture().With("one", folder: @"C:\dev\git\Theseus");
        fixture.Board.Refresh();
        fixture.Board.ToggleRow(fixture.Board.Rows.Single(row => row.Area == "Dev"));

        fixture.Board.RowsBy = BoardRowsBy.Project;
        fixture.Board.ToggleRow(fixture.Board.Rows.Single());

        Assert.That(fixture.Settings.ViewFolds["rows:Project"], Is.EqualTo(new[] { "Theseus" }));
        Assert.That(fixture.Settings.CollapsedAreas, Is.EqualTo(new[] { "Dev" }), "the area view's folds are left alone");

        fixture.Board.RowsBy = BoardRowsBy.Area;

        Assert.That(fixture.Board.Rows.Single(row => row.Area == "Dev").IsCollapsed, Is.True);
        Assert.That(fixture.Reopen() is var reopened && reopened.Rows.Single(row => row.Area == "Dev").IsCollapsed, Is.True);
    }

    [Test]
    public void Dragging_OnlyMovesStagesAndOnlyChangesAreasWhenRowsAreAreas()
    {
        var fixture = new BoardFixture().With("one", "New", "Research", @"C:\dev\git\Theseus");
        fixture.Board.Refresh();
        fixture.Board.RowsBy = BoardRowsBy.Project;

        fixture.Board.Move("one", "In progress", "Theseus");

        Assert.That((fixture.Card("one")!.Column, fixture.Card("one")!.Area), Is.EqualTo(("In progress", "Research")),
            "a drop within a project row changes the stage and keeps the area");

        fixture.Board.ColumnsBy = BoardColumnsBy.SessionState;
        fixture.Board.Move("one", BoardView.Working, "Theseus");

        Assert.That(fixture.Card("one")!.Column, Is.EqualTo("In progress"), "a session's state is not a place to drop a card");
        Assert.That(fixture.Board.CanDrag, Is.False);
    }

    [Test]
    public void TheMenuTicksFollowTheView()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();

        fixture.Board.RowsByChain = true;
        fixture.Board.RowsByChain = false;

        Assert.That((fixture.Board.RowsByChain, fixture.Board.RowsByArea), Is.EqualTo((true, false)),
            "unticking the chosen view leaves it chosen");
    }

    private static BoardRowViewModel Row(BoardFixture fixture, string name) =>
        fixture.Board.Rows.Single(row => row.Area == name);

    private static string ColumnOf(BoardFixture fixture, string key) =>
        fixture.Board.Cells.Single(cell => cell.Cards.Any(card => card.Key == key)).Column;
}
