using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Selection reaches only what is shown (PR 2 review, item 1): on the board, the grid's cards and not a project's cards
/// behind its project card; in the project view, the view's cards. Switching between them unticks what leaves sight.
/// </summary>
[TestFixture]
public class ShownSelectionTests
{
    private static BoardFixture Board(bool projectReads = true)
    {
        var fixture = new BoardFixture().With("ad-hoc").With("other-ad-hoc").With("member");
        fixture.Board.Refresh();

        var member = fixture.Card("member")!;
        member.Workstream = member.Workstream with { Project = "p", Slug = "a-slug" };
        fixture.Board.Persist(member);

        if (projectReads)
        {
            fixture.Board.KeepOutOfGrid = card => card.Workstream.Project == "p";
        }

        fixture.Board.Refresh();

        return fixture;
    }

    private static string[] Ticked(BoardFixture fixture) =>
        [.. fixture.Board.Cards.Where(card => card.IsSelected).Select(card => card.Key).Order()];

    private static void OpenView(BoardFixture fixture) =>
        fixture.Board.SelectionOrder = () => [fixture.Card("member")!];

    [Test]
    public void OnTheBoard_SelectAll_TicksOnlyTheGridsCards()
    {
        var fixture = Board();

        fixture.Board.ChooseShown(true);

        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "ad-hoc", "other-ad-hoc" }));
        Assert.That(fixture.Board.ShownCards, Has.Count.EqualTo(2), "what the selection count is taken over");
    }

    [Test]
    public void OnTheBoard_Invert_TurnsOverOnlyTheGridsCards()
    {
        var fixture = Board();
        fixture.Card("ad-hoc")!.IsSelected = true;

        fixture.Board.InvertShown();

        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "other-ad-hoc" }));
    }

    [Test]
    public void OnTheBoard_SelectLive_TicksOnlyTheGridsLiveCards()
    {
        var fixture = Board();
        fixture.Card("ad-hoc")!.Lifecycle = SessionLifecycle.Running;
        fixture.Card("member")!.Lifecycle = SessionLifecycle.Running;

        fixture.Board.SelectLiveShown();

        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "ad-hoc" }));
    }

    [Test]
    public void InTheView_SelectAllInvertAndSelectLive_ActOnTheViewsCards()
    {
        var fixture = Board();
        OpenView(fixture);
        fixture.Card("member")!.Lifecycle = SessionLifecycle.Running;

        fixture.Board.ChooseShown(true);
        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "member" }));

        fixture.Board.InvertShown();
        Assert.That(Ticked(fixture), Is.Empty);

        fixture.Board.SelectLiveShown();
        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "member" }));
    }

    [Test]
    public void OpeningTheView_UnticksTheBoardsCards_AndClosingItUnticksTheViews()
    {
        var fixture = Board();
        fixture.Card("ad-hoc")!.IsSelected = true;

        OpenView(fixture);
        fixture.Board.DeselectUnshown();
        Assert.That(Ticked(fixture), Is.Empty);

        fixture.Card("member")!.IsSelected = true;
        fixture.Board.SelectionOrder = null;
        fixture.Board.DeselectUnshown();
        Assert.That(Ticked(fixture), Is.Empty);
    }

    [Test]
    public void WithNoProjects_SelectAll_TicksEveryCard_AsBefore()
    {
        var fixture = Board(projectReads: false);

        fixture.Board.ChooseShown(true);

        Assert.That(Ticked(fixture), Is.EqualTo(new[] { "ad-hoc", "member", "other-ad-hoc" }));
    }

    [Test]
    public void ACardInAFoldedRow_IsStillTickedBySelectAll_AsBefore()
    {
        var fixture = Board(projectReads: false);
        fixture.Board.ToggleRow(fixture.Board.Rows.First(row => row.Cells.Any(cell => cell.Cards.Count > 0)));

        fixture.Board.ChooseShown(true);

        Assert.That(Ticked(fixture), Has.Length.EqualTo(3));
    }
}
