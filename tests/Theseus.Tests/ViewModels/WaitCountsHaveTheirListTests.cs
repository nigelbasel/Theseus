using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Every "waiting for you" mark has its list (project-view-usability item 6). On BuildYourMarket MCP (2026-11-23) objectives
/// showed ◆ Waiting for you and Nigel could find nothing to act on: a build whose verifier was already running counted
/// as waiting on him, and no line in Waits for you named it. An objective now waits for him exactly when it has items of
/// Waits for you, and they show under it with their buttons.
/// </summary>
[TestFixture]
public class WaitCountsHaveTheirListTests
{
    private const string Verifier = "verify-identity";
    private const string Preconditions = "bym-mcp-preconditions";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static string CardOf(string piece) => BuildYourMarketCards.Single(entry => entry.Piece == piece).Card;

    private ProjectViewModel WithVerifier(string build, SessionLifecycle lifecycle, string? failed = null)
    {
        IReadOnlyList<PieceCard> cards =
            [.. BuildYourMarketBoard(), Card(Verifier, lifecycle, role: WorkstreamRole.Verifier) with { StartFailure = failed }];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, CardOf(build))];

        return _views.BuildYourMarket(cards: cards, links: links);
    }

    private ProjectViewModel WithReport(bool satisfied, int day = 5, ProjectViewActions? actions = null)
    {
        var report = new LinkReport(DateTimeOffset.UnixEpoch.AddDays(day), "session", "report.md", "Done again.", null);
        IReadOnlyList<Link> links =
        [
            .. BuildYourMarketLinks().Select(link => link.Kind == LinkKind.ReportsBack && link.From == CardOf(Preconditions)
                ? link with { LatestReport = report, SatisfiedUtc = satisfied ? link.SatisfiedUtc : null }
                : link),
        ];

        return _views.BuildYourMarket(links: links, actions: actions);
    }

    private static ObjectiveHeadingViewModel ObjectiveOf(ProjectViewModel view, string piece) =>
        view.AllObjectives.Single(heading => heading.Pieces.Any(node => node.Name == piece));

    private static void AssertEveryMarkHasItsList(ProjectViewModel view)
    {
        foreach (var heading in view.AllObjectives.Where(heading => heading.Rollup is not null))
        {
            Assert.That(heading.Rollup!.State == RollupState.WaitingForYou, Is.EqualTo(heading.Waits.Count > 0),
                $"{heading.Title}: the mark says {heading.Rollup.State} with {heading.Waits.Count} items under it");
            Assert.That(heading.Waits.Select(wait => wait.Item), Is.SubsetOf(view.Waits.Select(wait => wait.Item)),
                $"{heading.Title}: every item under it is in Waits for you too");
        }
    }

    [Test]
    public void ARunningVerifier_LeavesItsBuildNotWaitingForYou()
    {
        var view = WithVerifier("bym-mcp-identity", SessionLifecycle.Running);
        var identity = view.AllNodes.Single(node => node.Name == "bym-mcp-identity").Piece;

        Assert.That(identity.Status.WaitsForYou, Is.False, "its check is under way; nothing waits on him");
        Assert.That(identity.Status.WhoActs, Does.Contain(Verifier));
        AssertEveryMarkHasItsList(view);
    }

    [Test]
    public void AnAskingVerifier_IsAWaitForYou_AimedAtTheVerifier()
    {
        var view = WithVerifier("bym-mcp-identity", SessionLifecycle.NeedsAttention);
        var line = ObjectiveOf(view, "bym-mcp-identity").Waits.Single(wait => wait.Item.Kind == WaitKind.Answer);

        Assert.That(line.Item.CardKey, Is.EqualTo(Verifier), "Answer it focuses the verifier, which is the one asking");
        AssertEveryMarkHasItsList(view);
    }

    [Test]
    public void AVerifierWhoseStartFailed_IsListedUnderItsObjective_WithStartAgain()
    {
        var view = WithVerifier("bym-mcp-identity", SessionLifecycle.NotStarted, failed: "its tab never ran");
        var heading = ObjectiveOf(view, "bym-mcp-identity");

        Assert.That(heading.Waits.Select(wait => wait.Item.Kind), Does.Contain(WaitKind.StartVerifier));
        AssertEveryMarkHasItsList(view);
    }

    [Test]
    public void AReportToConfirm_MakesItsObjectiveWait_AndIsListedUnderIt_WithItsConfirm()
    {
        var view = WithReport(satisfied: false);
        var heading = ObjectiveOf(view, Preconditions);
        var line = heading.Waits.Single(wait => wait.Item.Kind == WaitKind.ConfirmReport);

        Assert.That(heading.Rollup!.State, Is.EqualTo(RollupState.WaitingForYou));
        Assert.That(line.Button, Is.EqualTo("Confirm the report…"));
        Assert.That(line.Text, Does.Contain("reported back"));
        AssertEveryMarkHasItsList(view);
    }

    [Test]
    public void ALaterReportOnASatisfiedLink_IsCountedNowhere()
    {
        var view = WithReport(satisfied: true);

        Assert.That(view.Waits.Select(wait => wait.Item.Kind), Has.None.EqualTo(WaitKind.ConfirmReport));
        Assert.That(ObjectiveOf(view, Preconditions).Waits, Is.Empty);
        AssertEveryMarkHasItsList(view);
    }

    [Test]
    public void ALaterReportOnASatisfiedLink_ShowsOnItsBox_WithOpenReport_ButIsNotCounted()
    {
        // Nigel, 2026-11-23: "Show it on the box, not counted": it may be a correction he would otherwise never see.
        var view = WithReport(satisfied: true);
        var box = view.AllNodes.Single(node => node.Name == Preconditions);

        box.OpenLaterReport!.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(box.ReportedAgain, Does.StartWith("Reported again after you confirmed"));
            Assert.That(_views.Ran, Is.EqualTo(new[] { "Open:report.md" }));
            Assert.That(view.Waits.Select(wait => wait.Item.Kind), Has.None.EqualTo(WaitKind.ConfirmReport));
        });
    }

    [Test]
    public void AReportFiledBeforeTheConfirmation_SaysNothingMore()
    {
        // The fixture confirms on day 2; a report of day 1 is the one he confirmed, not a later one.
        var box = WithReport(satisfied: true, day: 1).AllNodes.Single(node => node.Name == Preconditions);

        Assert.That(box.ReportedAgain, Is.Null);
    }

    [Test]
    public void OpeningTheLaterReport_ClearsTheLine()
    {
        // Nigel, 2026-11-23: "Opening the report clears it"; it comes back only for a report filed after that.
        var opened = new List<(string Link, DateTimeOffset At, string Document)>();
        var actions = _views.Actions with { OpenReport = (link, at, document) => opened.Add((link, at, document)) };
        var box = WithReport(satisfied: true, actions: actions).AllNodes.Single(node => node.Name == Preconditions);

        box.OpenLaterReport!.Execute(null);
        var after = WithReport(satisfied: true, actions: actions with { ReportOpened = (_, _) => true })
            .AllNodes.Single(node => node.Name == Preconditions);

        Assert.Multiple(() =>
        {
            Assert.That(opened.Single().Document, Is.EqualTo("report.md"), "the link's own document");
            Assert.That(after.ReportedAgain, Is.Null);
        });
    }

    [Test]
    public void AConfirmedReportWithNothingLater_SaysNothingMore()
    {
        var box = _views.BuildYourMarket().AllNodes.Single(node => node.Name == Preconditions);

        Assert.That(box.ReportedAgain, Is.Null);
    }

    [Test]
    public void TheFixture_AsItStands_HasEveryMarkWithItsList()
    {
        AssertEveryMarkHasItsList(_views.BuildYourMarket());
    }

    [Test]
    public void ASearchThatFoundOnlyAVerifier_HighlightsTheBoxItChecks()
    {
        // Project-view-usability item 4, review: "Verify: Theseus" lit the project and then highlighted nothing in it.
        var view = WithVerifier("bym-mcp-identity", SessionLifecycle.NotStarted);
        var box = view.AllNodes.Single(node => node.Name == "bym-mcp-identity");
        var filter = new Theseus.Core.Finding.CardFilter(Theseus.Core.Finding.SearchQuery.Parse("Verify"), [], Theseus.Core.Finding.TagMatch.Any);

        Assert.That(SearchCarry.Matches(box, key => key == Verifier, filter), Is.True);
    }
}
