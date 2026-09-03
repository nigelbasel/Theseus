using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// What a refresh must not throw away.
///
/// A refresh rebuilds every card view model from scratch, and it runs on a timer as well as after
/// every start, stop and archive. Anything the user has set on a card — which ones are ticked, which
/// are folded — therefore has to be carried across by key, or it empties itself seconds after being
/// set and the feature is unusable.
///
/// Both of those were caught by reading the code rather than by a test, because there were no view
/// model tests to catch them. These are those tests.
/// </summary>
[TestFixture]
public class BoardViewModelTests
{
    [Test]
    public void Refresh_PutsEachCardInItsOwnStageAndArea()
    {
        var fixture = new BoardFixture().With("research-one", "New", "Research")
            .With("dev-one", "In progress", "Dev");

        fixture.Board.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Card("research-one")!.Column, Is.EqualTo("New"));
            Assert.That(fixture.Card("research-one")!.Area, Is.EqualTo("Research"));
            Assert.That(fixture.Card("dev-one")!.Column, Is.EqualTo("In progress"));
            Assert.That(fixture.Card("dev-one")!.Area, Is.EqualTo("Dev"));
        });
    }

    [Test]
    public void Refresh_KeepsTheTicks()
    {
        var fixture = new BoardFixture().With("one").With("two");

        fixture.Board.Refresh();
        fixture.Card("one")!.IsSelected = true;

        fixture.Board.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Card("one")!.IsSelected, Is.True,
                "a refresh runs on a timer; dropping the ticks empties a selection before it can be used");
            Assert.That(fixture.Card("two")!.IsSelected, Is.False, "and does not tick anything else");
        });
    }

    [Test]
    public void Refresh_KeepsTheFoldedCardsFolded()
    {
        var fixture = new BoardFixture().With("one").With("two");

        fixture.Board.Refresh();
        fixture.Card("one")!.IsCollapsed = true;

        fixture.Board.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Card("one")!.IsCollapsed, Is.True,
                "a refresh that unfolded the board would undo the view a moment after it was asked for");
            Assert.That(fixture.Card("two")!.IsCollapsed, Is.False);
        });
    }

    [Test]
    public void Refresh_CarriesTicksAndFoldsIndependently()
    {
        var fixture = new BoardFixture().With("one").With("two");

        fixture.Board.Refresh();
        fixture.Card("one")!.IsSelected = true;
        fixture.Card("two")!.IsCollapsed = true;

        fixture.Board.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(fixture.Card("one")!.IsSelected, Is.True);
            Assert.That(fixture.Card("one")!.IsCollapsed, Is.False);
            Assert.That(fixture.Card("two")!.IsSelected, Is.False);
            Assert.That(fixture.Card("two")!.IsCollapsed, Is.True);
        });
    }

    [Test]
    public void Refresh_ForgetsACardThatHasGoneRatherThanCarryingItsTickToANewOne()
    {
        var fixture = new BoardFixture().With("one");

        fixture.Board.Refresh();
        fixture.Card("one")!.IsSelected = true;

        var second = new BoardFixture().With("one");

        second.Board.Refresh();

        Assert.That(second.Card("one")!.IsSelected, Is.False,
            "a tick belongs to a board that is open, not to a key that comes back later");
    }

    [Test]
    public void Refresh_SendsACardWhoseStageIsNotConfiguredToTheFirstOneRatherThanLosingIt()
    {
        var fixture = new BoardFixture(lanes: ["New", "Done"]).With("stray", "Nowhere");

        fixture.Board.Refresh();

        Assert.That(fixture.Card("stray"), Is.Not.Null, "a card must never vanish off the board");
        Assert.That(fixture.Card("stray")!.Column, Is.EqualTo("New"));
    }

    [Test]
    public void Refresh_SendsACardWhoseAreaIsNotConfiguredToTheFirstRow()
    {
        var fixture = new BoardFixture(areas: ["Research", "Dev"]).With("stray", "New", "Retired");

        fixture.Board.Refresh();

        Assert.That(fixture.Card("stray")!.Area, Is.EqualTo("Research"));
    }

    [Test]
    public void Refresh_SaysHowManyWorkstreamsItRead()
    {
        var fixture = new BoardFixture().With("one").With("two").With("three");

        fixture.Board.Refresh();

        Assert.That(fixture.Board.Status, Does.Contain("3"));
    }

    [Test]
    public void Cards_AreEmptyBeforeTheFirstRefresh()
    {
        Assert.That(new BoardFixture().With("one").Board.Cards, Is.Empty,
            "the board reads the disk when it is told to, not when it is built");
    }
}
