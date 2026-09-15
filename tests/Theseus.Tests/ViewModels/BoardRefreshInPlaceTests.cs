namespace Theseus.Tests.ViewModels;

/// <summary>
/// A refresh changes the cards that are there instead of throwing them away.
///
/// It used to empty every cell and fill it again with new card objects. Every one of those is a
/// change to the window's element tree, and WPF reports each to any UI Automation listener on the
/// machine and waits while it does. On 2026-09-15 a dump of the frozen board showed the UI thread
/// stuck in exactly that report, several seconds at a time. A board where nothing has changed now
/// changes nothing on screen.
/// </summary>
[TestFixture]
public class BoardRefreshInPlaceTests
{
    [Test]
    public void Refresh_KeepsEveryCardWhenNothingHasChanged()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();
        var before = fixture.Board.Cards.ToList();

        fixture.Board.Refresh();
        var after = fixture.Board.Cards.ToList();

        Assert.That(after, Has.Count.EqualTo(before.Count));
        Assert.That(after.Zip(before).All(pair => ReferenceEquals(pair.First, pair.Second)), Is.True,
            "a card replaced by an identical one is still a change to the screen");
    }

    [Test]
    public void Refresh_ChangesNothingOnScreenWhenNothingHasChanged()
    {
        var fixture = new BoardFixture().With("one").With("two").With("three", "Done", "Dev");
        fixture.Board.Refresh();
        var changes = CountChanges(fixture);

        fixture.Board.Refresh();

        Assert.That(changes(), Is.Zero);
    }

    [Test]
    public void Refresh_UpdatesACardInPlaceWhenItsPlanChanges()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        var card = fixture.Card("one");

        fixture.Retitle("one", "A better name").Board.Refresh();

        Assert.That(fixture.Card("one"), Is.SameAs(card));
        Assert.That(card!.Title, Is.EqualTo("A better name"));
    }

    [Test]
    public void Refresh_MovesACardToTheStageItNowBelongsTo()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();
        var card = fixture.Card("one");

        fixture.MoveTo("one", "Done").Board.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Card("one"), Is.SameAs(card));
            Assert.That(card!.Column, Is.EqualTo("Done"));
            Assert.That(Cell(fixture, "New").Cards, Does.Not.Contain(card));
            Assert.That(Cell(fixture, "Done").Cards, Does.Contain(card));
        });
    }

    [Test]
    public void Refresh_TakesAwayOnlyTheCardWhosePlanHasGone()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();
        var kept = fixture.Card("one");

        fixture.Without("two").Board.Refresh();

        Assert.That(fixture.Card("two"), Is.Null);
        Assert.That(fixture.Card("one"), Is.SameAs(kept));
    }

    [Test]
    public void Refresh_AddsANewWorkstreamWithoutDisturbingTheOthers()
    {
        var fixture = new BoardFixture().With("one").With("two");
        fixture.Board.Refresh();
        var changes = CountChanges(fixture);

        fixture.With("three").Board.Refresh();

        Assert.That(fixture.Card("three"), Is.Not.Null);
        Assert.That(changes(), Is.EqualTo(1), "one card arrived, so one thing on screen changed");
    }

    [Test]
    public void Refresh_KeepsATickOnACardThatMoved()
    {
        var fixture = new BoardFixture().With("one");
        fixture.Board.Refresh();
        fixture.Card("one")!.IsSelected = true;

        fixture.MoveTo("one", "In progress").Board.Refresh();

        Assert.That(fixture.Card("one")!.IsSelected, Is.True);
    }

    private static Func<int> CountChanges(BoardFixture fixture)
    {
        var changes = 0;

        fixture.Board.Rows.CollectionChanged += (_, _) => changes++;

        foreach (var cell in fixture.Board.Cells)
        {
            cell.Cards.CollectionChanged += (_, _) => changes++;
        }

        return () => changes;
    }

    private static Theseus.App.ViewModels.ColumnViewModel Cell(BoardFixture fixture, string column) =>
        fixture.Board.Cells.First(cell => cell.Column == column && cell.Area == "Research");
}
