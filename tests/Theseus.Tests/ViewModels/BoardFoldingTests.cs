using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Cards, rows and stages fold, the board remembers which across a restart, and anything folded
/// still says how many of its cards have a session running (Nigel, 2026-10-30).
/// </summary>
[TestFixture]
public class BoardFoldingTests
{
    [Test]
    public void Fold_IsRememberedAcrossARestart()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();

        fixture.Board.Fold(fixture.Card("one")!, folded: true);

        var reopened = fixture.Reopen();
        reopened.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(Card(reopened, "one").IsCollapsed, Is.True);
            Assert.That(Card(reopened, "two").IsCollapsed, Is.False);
        });
    }

    [Test]
    public void Unfolding_IsRememberedToo()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        fixture.Board.Fold(fixture.Card("one")!, folded: true);

        fixture.Board.Fold(fixture.Card("one")!, folded: false);

        Assert.That(fixture.Settings.CollapsedCards, Is.Empty);
    }

    [Test]
    public void RememberFolds_KeepsTheFoldOfACardNotOnTheBoardJustNow()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();
        fixture.Board.Fold(fixture.Card("two")!, folded: true);

        fixture.Without("two");
        fixture.Board.Refresh();
        fixture.Board.Fold(fixture.Card("one")!, folded: true);

        Assert.That(fixture.Settings.CollapsedCards, Is.EquivalentTo(new[] { "one", "two" }),
            "an archived card comes back folded the way it was put away");
    }

    [Test]
    public void ToggleStage_FoldsItInEveryRowAndIsRememberedAcrossARestart()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        var stage = fixture.Board.StageHeaders.Single(header => header.Stage == "In progress");

        fixture.Board.ToggleStage(stage);

        var reopened = fixture.Reopen();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Cells.Where(cell => cell.Column == "In progress").Select(cell => cell.IsCollapsed),
                Is.All.True);
            Assert.That(fixture.Board.Cells.Where(cell => cell.Column != "In progress").Select(cell => cell.IsCollapsed),
                Is.All.False);
            Assert.That(reopened.StageHeaders.Single(header => header.Stage == "In progress").IsCollapsed, Is.True);
            Assert.That(reopened.Cells.Where(cell => cell.Column == "In progress").Select(cell => cell.IsCollapsed),
                Is.All.True);
        });
    }

    [Test]
    public void UnfoldGrid_OpensEveryRowAndStageAndRemembersIt()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        fixture.Board.ToggleStage(fixture.Board.StageHeaders[0]);
        fixture.Board.ToggleRow(fixture.Board.Rows[0]);

        fixture.Board.UnfoldGrid();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Board.Rows.Select(row => row.IsCollapsed), Is.All.False);
            Assert.That(fixture.Board.StageHeaders.Select(header => header.IsCollapsed), Is.All.False);
            Assert.That(fixture.Board.Cells.Select(cell => cell.IsCollapsed), Is.All.False);
            Assert.That(fixture.Settings.CollapsedAreas, Is.Empty);
            Assert.That(fixture.Settings.CollapsedStages, Is.Empty);
        });
    }

    [Test]
    public void LiveCounts_FollowTheSessionsUnderAFold()
    {
        var fixture = new BoardFixture().With("one", "In progress", "Dev").With("two", "In progress", "Research")
            .With("three", "New", "Dev");
        fixture.Board.Refresh();
        var stage = fixture.Board.StageHeaders.Single(header => header.Stage == "In progress");
        var dev = fixture.Board.Rows.Single(row => row.Area == "Dev");
        var raised = new List<string?>();
        dev.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        fixture.Card("one")!.Lifecycle = SessionLifecycle.Running;
        fixture.Card("two")!.Lifecycle = SessionLifecycle.Idle;

        Assert.Multiple(() =>
        {
            Assert.That(stage.LiveCount, Is.EqualTo(2));
            Assert.That(dev.LiveCount, Is.EqualTo(1));
            Assert.That(dev.LiveText, Is.EqualTo("1 live"));
            Assert.That(raised, Does.Contain(nameof(BoardRowViewModel.LiveText)), "a folded row's mark updates on its own");
        });

        fixture.Card("one")!.Lifecycle = SessionLifecycle.Ended;

        Assert.That(dev.LiveText, Is.Empty);
    }

    private static WorkstreamCardViewModel Card(BoardViewModel board, string key) =>
        board.Cards.Single(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase));
}
