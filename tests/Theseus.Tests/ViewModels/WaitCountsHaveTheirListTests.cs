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

    private ProjectViewModel WithVerifier(string build, SessionLifecycle lifecycle)
    {
        IReadOnlyList<PieceCard> cards = [.. BuildYourMarketBoard(), Card(Verifier, lifecycle, role: WorkstreamRole.Verifier)];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, CardOf(build))];

        return _views.BuildYourMarket(cards: cards, links: links);
    }

    private ProjectViewModel WithReport(bool satisfied)
    {
        var report = new LinkReport(DateTimeOffset.UnixEpoch.AddDays(5), "session", "report.md", "Done again.", null);
        IReadOnlyList<Link> links =
        [
            .. BuildYourMarketLinks().Select(link => link.Kind == LinkKind.ReportsBack && link.From == CardOf(Preconditions)
                ? link with { LatestReport = report, SatisfiedUtc = satisfied ? link.SatisfiedUtc : null }
                : link),
        ];

        return _views.BuildYourMarket(links: links);
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
    public void AnUnstartedVerifier_IsListedUnderItsObjective_WithItsStart()
    {
        var view = WithVerifier("bym-mcp-identity", SessionLifecycle.NotStarted);
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
