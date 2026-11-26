using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A closed or archived project leaves the board's projects and is found under Archived, with Reopen (archive-a-project,
/// 2026-11-26); its cards stay out of the grid, since its project card stands for them there too.
/// </summary>
[TestFixture]
public class BoardClosedProjectTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private static (BoardFixture Fixture, ProjectCardViewModel Open, ProjectCardViewModel Closed) Board()
    {
        var fixture = new BoardFixture().With("ad-hoc").With("member");
        fixture.Board.Refresh();
        fixture.Board.KeepOutOfGrid = card => card.Key == "member";
        var open = new ProjectCardViewModel("open-project", "Open project");
        var closed = new ProjectCardViewModel("closed-project", "Closed project")
        {
            Closure = new ProjectClosure(ProjectClosureKind.Closed, Nine),
        };
        fixture.Board.ShowProjects([open, closed]);

        return (fixture, open, closed);
    }

    private static IReadOnlyList<ProjectCardViewModel> OnScreen(BoardFixture fixture) =>
        [.. fixture.Board.Cells.SelectMany(cell => cell.Projects)];

    [Test]
    public void AClosedProject_IsNotOnTheBoard()
    {
        var (fixture, open, _) = Board();

        Assert.That(OnScreen(fixture), Is.EqualTo(new[] { open }));
    }

    [Test]
    public void UnderArchived_OnlyThePutAwayProjectsShow()
    {
        var (fixture, _, closed) = Board();

        fixture.Board.ShowArchived = true;

        Assert.That(OnScreen(fixture), Is.EqualTo(new[] { closed }));
    }

    [Test]
    public void APutAwayProject_SaysHowItWasPutAway_AndOffersReopen()
    {
        var closed = new ProjectCardViewModel("p", "P") { Closure = new ProjectClosure(ProjectClosureKind.Archived, Nine) };
        var open = new ProjectCardViewModel("q", "Q");

        Assert.That((closed.IsPutAway, closed.ClosedText, closed.ShowsRunButtons), Is.EqualTo((true, "Archived", false)),
            "a put-away project starts and stops nothing until it is reopened");
        Assert.That((open.IsPutAway, open.ClosedText), Is.EqualTo((false, string.Empty)));
    }
}
