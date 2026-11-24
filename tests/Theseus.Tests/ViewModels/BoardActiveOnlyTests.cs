using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Project cards fold as cards do, and "Show active only" compresses the board to what is active (git-22 for Nigel,
/// 2026-11-24): a card with a session, or with something on it waiting on the owner; a project when any of its cards
/// is, or when something in it waits on the owner. The choice is remembered, and the search still finds what it hides.
/// </summary>
[TestFixture]
public class BoardActiveOnlyTests
{
    private static BoardFixture Board()
    {
        BoardFixture fixture = new BoardFixture().With("live").With("quiet").With("member").With("other-member");
        fixture.Board.Refresh();
        fixture.Card("live")!.Lifecycle = SessionLifecycle.Idle;
        fixture.Card("member")!.Workstream = fixture.Card("member")!.Workstream with { Project = "busy" };
        fixture.Card("member")!.Lifecycle = SessionLifecycle.Running;
        fixture.Card("other-member")!.Workstream = fixture.Card("other-member")!.Workstream with { Project = "still" };
        fixture.Board.ShowProjects([new ProjectCardViewModel("busy", "Busy"), new ProjectCardViewModel("still", "Still")]);

        return fixture;
    }

    private static List<string> Shown(BoardFixture fixture) =>
        [.. fixture.Board.ShownItems.Select(item => item.Title)];

    [Test]
    public void FoldInactive_FoldsAProjectWithNoSession_AndLeavesABusyOneOpen()
    {
        BoardFixture fixture = Board();

        fixture.Board.FoldInactive();

        Assert.That(Project(fixture, "still").IsCollapsed, Is.True);
        Assert.That(Project(fixture, "busy").IsCollapsed, Is.False);
        Assert.That(fixture.Card("quiet")!.IsCollapsed, Is.True);
        Assert.That(fixture.Card("live")!.IsCollapsed, Is.False);
    }

    [Test]
    public void FoldAll_AndUnfoldAll_FoldAndOpenProjectCardsToo()
    {
        BoardFixture fixture = Board();

        fixture.Board.FoldAll();

        Assert.That(fixture.Board.ProjectCards.Select(project => project.IsCollapsed), Is.All.True);

        fixture.Board.UnfoldAll();

        Assert.That(fixture.Board.ProjectCards.Select(project => project.IsCollapsed), Is.All.False);
        Assert.That(fixture.Board.Cards.Select(card => card.IsCollapsed), Is.All.False);
    }

    [Test]
    public void AProjectsFold_IsRemembered()
    {
        BoardFixture fixture = Board();

        fixture.Board.Fold(Project(fixture, "still"), true);

        Assert.That(fixture.Settings.CollapsedProjects, Is.EqualTo(new[] { "still" }));
    }

    [Test]
    public void ActiveOnly_ShowsOnlyCardsWithASession_AndProjectsWithAnActiveCard()
    {
        BoardFixture fixture = Board();

        fixture.Board.ShowsActiveOnly = true;

        Assert.That(Shown(fixture), Is.EquivalentTo(new[] { "live", "member", "Busy" }));
    }

    [Test]
    public void ACardWaitingOnTheOwner_IsActive()
    {
        BoardFixture fixture = Board();
        fixture.Board.WaitsOnOwner = key => key == "quiet";

        fixture.Board.ShowsActiveOnly = true;

        Assert.That(Shown(fixture), Does.Contain("quiet"));
    }

    [Test]
    public void ActiveOnly_IsRemembered_AndSaidInTheStatusBar()
    {
        BoardFixture fixture = Board();

        fixture.Board.ShowsActiveOnly = true;

        Assert.That(fixture.Settings.ShowActiveOnly, Is.True);
        Assert.That(fixture.Board.ActiveOnlyNoteInStatus, Is.EqualTo("  ·  Showing 3 of 6 cards · active only"));
        Assert.That(fixture.Reopen().ShowsActiveOnly, Is.True);

        fixture.Board.ToggleActiveOnlyCommand.Execute(null);

        Assert.That(fixture.Board.ShowsActiveOnly, Is.False);
        Assert.That(fixture.Board.ActiveOnlyNoteInStatus, Is.Empty);
    }

    [Test]
    public void TheSearch_StillFindsAHiddenCard_AndSaysTheFilterHidesIt()
    {
        BoardFixture fixture = Board();
        fixture.Board.ShowsActiveOnly = true;

        fixture.Board.Search.Text = "quiet";

        Assert.That(fixture.Board.Search.StatusText, Does.Contain("1 more hidden by Active only"));
    }

    private static ProjectCardViewModel Project(BoardFixture fixture, string name) =>
        fixture.Board.ProjectCards.Single(project => project.Name == name);
}
