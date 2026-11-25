using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Why a card is stopped (1.1e AC 12, 13): each reason's line on the card's status line, its ▶, a done card muted; the
/// question asked before one card of a running project is stopped or started by hand (AC 10).
/// </summary>
[TestFixture]
public class WhyStoppedTests
{
    private static readonly DateTimeOffset Reported = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private static WorkstreamCardViewModel Card(string key = "a-card") => new(ProjectCards.Card(key));

    private static Link ConfirmedReport(string from) =>
        new("l1", LinkKind.ReportsBack, from, "parent", Reported, Link.MadeOnTheBoard, SatisfiedUtc: Reported,
            SatisfiedBy: LinkBook.ConfirmedReport);

    private static WhyStopped? Reason(WorkstreamCardViewModel card, IReadOnlyDictionary<string, IReadOnlyList<string>>? runStates = null,
        IReadOnlyCollection<string>? byOwner = null, IReadOnlyList<Link>? links = null) =>
        CardStopReasons.For(card, runStates ?? new Dictionary<string, IReadOnlyList<string>>(), name => $"Title of {name}",
            byOwner ?? [], links ?? []);

    [Test]
    public void AConfirmedReportBack_IsDone_WithItsDate()
    {
        var reason = Reason(Card(), links: [ConfirmedReport("a-card")]);

        Assert.That((reason!.Kind, reason.Line), Is.EqualTo((WhyStoppedKind.Done, "✓ Done: reported back " + Reported.ToLocalTime().ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture))));
    }

    [Test]
    public void AReportOnlyMade_OrToAnotherCard_IsNotDone()
    {
        var made = ConfirmedReport("a-card") with { SatisfiedUtc = null, SatisfiedBy = null };

        Assert.That(Reason(Card(), links: [made, ConfirmedReport("other")]), Is.Null);
    }

    [Test]
    public void TheLastStage_NotRunning_IsDone()
    {
        var card = Card();
        card.IsInLastStage = true;

        Assert.That(Reason(card)!.Line, Is.EqualTo("✓ Done"));
    }

    [Test]
    public void InAStoppedProjectsRunState_ItResumesWithTheProject()
    {
        var reason = Reason(Card(), new Dictionary<string, IReadOnlyList<string>> { ["p"] = ["a-card"] });

        Assert.That((reason!.Kind, reason.Line), Is.EqualTo((WhyStoppedKind.ProjectStopped, "⏸ Project stopped: resumes with Title of p")));
    }

    [Test]
    public void StoppedByHand_WhileItsProjectRan_SaysStoppedByYou()
    {
        Assert.That(Reason(Card(), byOwner: ["a-card"])!.Line, Is.EqualTo("Stopped by you"));
    }

    [Test]
    public void ARunningCard_HasNoReason_AndAPlainOne_KeepsItsOwnLine()
    {
        var running = Card();
        running.Lifecycle = SessionLifecycle.Running;

        Assert.That(Reason(running, byOwner: ["a-card"], links: [ConfirmedReport("a-card")]), Is.Null);
        Assert.That(Reason(Card()), Is.Null);
        Assert.That(Card().StatusLine, Is.EqualTo("never started"));
    }

    [Test]
    public void TheReason_IsTheCardsStatusLine_AndChangesWhatItsPlayButtonSays()
    {
        var done = Card();
        done.StopReason = Reason(done, links: [ConfirmedReport("a-card")]);
        var stopped = Card();
        stopped.StopReason = Reason(stopped, new Dictionary<string, IReadOnlyList<string>> { ["p"] = ["a-card"] });

        Assert.That((done.StatusLine, done.PrimaryAction, done.IsDone), Is.EqualTo((done.StopReason!.Line, "Restart for more work", true)));
        Assert.That(done.PlayTooltip, Is.EqualTo("Restart for more work: asks what the more work is"));
        Assert.That((stopped.StatusLine, stopped.PrimaryAction, stopped.IsDone), Is.EqualTo((stopped.StopReason!.Line, "Start its project", false)));
    }

    [Test]
    public void ADoneCardInAProject_IsAPlainRestart_AskingNothing()
    {
        var done = Card();
        done.Workstream = done.Workstream with { Project = "theseus" };
        done.StopReason = Reason(done, links: [ConfirmedReport("a-card")]);

        Assert.Multiple(() =>
        {
            Assert.That(done.IsDone, Is.True);
            Assert.That(done.PrimaryAction, Is.Not.EqualTo("Restart for more work"), "sessions-run-the-project item 17");
            Assert.That(done.PlayTooltip, Does.Not.Contain("asks what the more work is"));
        });
    }

    [Test]
    public void StoppingACardOfARunningProject_AsksFirst_InTheSpecsWords()
    {
        var project = new ProjectCardViewModel("p", "Theseus bootstrap") { IsLive = true, MemberKeys = ["a-card"] };

        Assert.That(ProjectCardGuard.Question([Card()], [project], "Stop"),
            Is.EqualTo("a-card is part of Theseus bootstrap, which decides what runs. Stop it anyway?"));
        Assert.That(ProjectCardGuard.Question([Card()], [project], "Start"), Does.EndWith("Start it anyway?"));
    }

    [Test]
    public void ACardOfAStoppedProject_OrAnAdHocCard_IsNotAskedAbout()
    {
        var stopped = new ProjectCardViewModel("p", "P") { IsLive = true, MemberKeys = ["a-card"], RunState = ["a-card"] };
        var idle = new ProjectCardViewModel("q", "Q") { IsLive = false, MemberKeys = ["a-card"] };

        Assert.That(ProjectCardGuard.Question([Card()], [stopped, idle], "Stop"), Is.Null);
        Assert.That(ProjectCardGuard.Question([Card("ad-hoc")], [new ProjectCardViewModel("r", "R") { IsLive = true, MemberKeys = ["x"] }], "Stop"),
            Is.Null);
    }
}
