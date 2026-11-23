using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A build that already has a verifier card gets no second one (Nigel, 2026-11-23: Start the checks… stayed after it made
/// the verifier cards, so a second press made a duplicate of each). A verifier stands from the moment it is made until it
/// reports or is archived, whatever its session is doing: a tab waiting at Claude Code's question about trusting its
/// folder reads as ended, which once let a second press make a copy (review of the auto-start, must fix 1). Theseus starts
/// a verifier itself as it makes one, so the Waits line names one only when it is neither running nor starting nor
/// reported, with Start again: after a failed start, a lapsed ticket, a session that exited at once, or a restart (must
/// fix 2 and 3).
/// </summary>
[TestFixture]
public class NoDuplicateVerifierTests
{
    private const string Verifier = "verify-identity";
    private const string WhyNot = "Windows Terminal opened the tab but never ran the session in it";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;
    private string _build = string.Empty;
    private string _buildCard = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        _build = _views.BuildYourMarket().AllNodes.First(node => node.Piece.Status.State == StageNodeState.HoldoutsNotRun).Name;
        _buildCard = BuildYourMarketCards.Single(entry => entry.Piece == _build).Card;
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectViewModel WithVerifier(SessionLifecycle lifecycle, bool archived = false, bool hasRun = false, string? failed = null,
        bool starting = false, bool reported = false)
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            Card(Verifier, lifecycle, role: WorkstreamRole.Verifier) with
            {
                IsArchived = archived, HasRun = hasRun, StartFailure = failed, IsStarting = starting,
            },
        ];
        var reportBack = NewLink(LinkKind.ReportsBack, Verifier, _buildCard);
        IReadOnlyList<Link> links =
        [
            .. BuildYourMarketLinks(),
            reported ? reportBack with { LatestReport = new LinkReport(DateTimeOffset.UnixEpoch, "s", "r.md", "x: pass", "done") } : reportBack,
        ];

        return _views.BuildYourMarket(cards: cards, links: links);
    }

    private static IReadOnlyList<WaitItemViewModel> Of(ProjectViewModel view, WaitKind kind) =>
        [.. view.Waits.Where(wait => wait.Item.Kind == kind)];

    // Whether a verifier still stands for the build, so no second one is made: what "Start the checks…" left out until
    // Theseus started checks itself and the line went (git-22, 2026-11-23).
    private bool Stands(ProjectViewModel view) => view.AllNodes.Single(node => node.Name == _build).Piece.PendingVerifiers.Count > 0;

    [Test]
    public void AStandingVerifier_StandsForItsBuild_AndNothingAsksYouToStartChecks()
    {
        var view = WithVerifier(SessionLifecycle.NotStarted, starting: true);

        Assert.That(Stands(view), Is.True);
        Assert.That(view.Waits.Select(wait => wait.Button), Has.None.EqualTo("Start the checks…"), "Theseus starts checks itself (git-22, 2026-11-23)");
    }

    [Test]
    public void AVerifierWhoseTabWaitsToBeTrusted_StillStands_SoNoCopyIsMade()
    {
        var view = WithVerifier(SessionLifecycle.Ended, hasRun: true, starting: true);

        Assert.That(Stands(view), Is.True, "review of the auto-start, must fix 1: its run read as ended, and a second press made a -2 copy");
        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty, "it is starting: nothing for you to do");
    }

    [Test]
    public void AVerifierBeingStarted_NeedsNothingFromYou()
    {
        var view = WithVerifier(SessionLifecycle.NotStarted, starting: true);

        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty, "Theseus starts it (Nigel, 2026-11-23): no \"only you start it\"");
        Assert.That(view.AllNodes.Single(node => node.Name == _build).Actions.Select(action => action.Label),
            Has.None.Contains(Verifier), "no Start on its build's box");
    }

    [Test]
    public void ARunningVerifier_NeedsNothingFromYou_AndItsBuildIsNotOfferedAgain()
    {
        var view = WithVerifier(SessionLifecycle.Running, hasRun: true);

        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty);
        Assert.That(Stands(view), Is.True);
    }

    [Test]
    public void AVerifierWhoseStartFailed_SaysWhyInTheWaitsLine_WithStartAgain_WhichStartsIt()
    {
        var line = Of(WithVerifier(SessionLifecycle.NotStarted, failed: WhyNot), WaitKind.StartVerifier).Single();

        Assert.That(line.Button, Is.EqualTo("Start again"));
        Assert.That(line.Text, Is.EqualTo($"{Verifier} is not running and has not reported: {WhyNot}."));

        line.Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { $"StartVerifier:{Verifier}" }));
    }

    [TestCase(SessionLifecycle.NotStarted, false)]
    [TestCase(SessionLifecycle.Ghost, true)]
    [TestCase(SessionLifecycle.Ended, true)]
    public void AVerifierNeitherRunningNorStartingNorReported_OffersStartAgain_EvenWithNoReasonKnown(SessionLifecycle lifecycle, bool hasRun)
    {
        var line = Of(WithVerifier(lifecycle, hasRun: hasRun), WaitKind.StartVerifier).Single();

        Assert.That(line.Text, Is.EqualTo($"{Verifier} is not running and has not reported."),
            "review of the auto-start, must fix 2 and 3: after a restart, a lapsed ticket or a session that exited at once it was stuck");
        Assert.That(line.Button, Is.EqualTo("Start again"));
    }

    [Test]
    public void AVerifierThatCannotRunUnderTodaysRules_NeverAsksForAStart_AndDoesNotStand()
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            Card(Verifier, SessionLifecycle.NotStarted, role: WorkstreamRole.Verifier) with { IsObsolete = true },
        ];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, _buildCard)];
        var view = _views.BuildYourMarket(cards: cards, links: links);

        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty,
            "git-22, 2026-11-23: a verifier made before Checks lines showed Start again and was counted as waiting for him");
        Assert.That(Stands(view), Is.False, "it is Clear up's, and a new one can be made");
    }

    [Test]
    public void EveryBuildWithARunningVerifier_AsksNothingOfYou()
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            .. BuildYourMarketCards.Select(entry => Card($"verify-{entry.Card}", SessionLifecycle.Running, role: WorkstreamRole.Verifier)),
        ];
        IReadOnlyList<Link> links =
        [
            .. BuildYourMarketLinks(),
            .. BuildYourMarketCards.Select(entry => NewLink(LinkKind.ReportsBack, $"verify-{entry.Card}", entry.Card)),
        ];
        var view = _views.BuildYourMarket(cards: cards, links: links);

        Assert.That(view.Waits.Select(wait => wait.Button), Has.None.EqualTo("Start the checks…"));
        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty, "each is running");
    }

    [Test]
    public void AReportedOrArchivedVerifier_LetsTheBuildBeCheckedAgain()
    {
        Assert.That(Stands(WithVerifier(SessionLifecycle.Ended, hasRun: true, reported: true)), Is.False);
        Assert.That(Stands(WithVerifier(SessionLifecycle.NotStarted, archived: true)), Is.False);
    }

    [Test]
    public void AVerifierNotRunning_HasStartAgain_OnItsBuildsBox_AndItsVerifyStage()
    {
        var box = WithVerifier(SessionLifecycle.NotStarted, failed: WhyNot).AllNodes.Single(node => node.Name == _build);
        var start = box.Actions.Single(action => action.Label == $"Start {Verifier} again");
        var inMenu = box.Stages.Single(stage => stage.Node.Stage == PipelineStage.Verify).MenuItems.Single(item => item.Label == start.Label);

        Assert.That((start.IsEnabled, inMenu.IsEnabled), Is.EqualTo((true, true)));

        start.Command.Execute(null);
        inMenu.Command.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { $"StartVerifier:{Verifier}", $"StartVerifier:{Verifier}" }));
    }

    [Test]
    public void ARunningVerifier_OffersNoStart_OnItsBuildsBox()
    {
        var box = WithVerifier(SessionLifecycle.Running, hasRun: true).AllNodes.Single(node => node.Name == _build);

        Assert.That(box.Actions.Select(action => action.Label), Has.None.Contains(Verifier));
    }

    // The live case (git-22, 2026-11-23): "Verify: Theseus" checks a piece that is Done, not awaiting its check.
    [Test]
    public void AVerifierNotRunning_OfAPieceThatIsDone_IsStillInTheWaitsLine_AndOnItsVerifyStage()
    {
        var done = _views.BuildYourMarket().AllNodes.First(node => node.Piece is { IsCurrentPiece: true, Card: not null }
                                                                   && node.Piece.Status.State == StageNodeState.Done
                                                                   && node.Piece.Stages.Any(stage => stage.Stage == PipelineStage.Verify));
        IReadOnlyList<PieceCard> cards = [.. BuildYourMarketBoard(), Card(Verifier, role: WorkstreamRole.Verifier)];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, done.Piece.CardKey)];
        var view = _views.BuildYourMarket(cards: cards, links: links);
        var box = view.AllNodes.Single(node => node.Name == done.Name);

        Assert.That(box.Piece.Status.State, Is.EqualTo(StageNodeState.Done));
        Assert.That(Of(view, WaitKind.StartVerifier).Single().Item.CardKey, Is.EqualTo(Verifier));
        Assert.That(box.Stages.Single(stage => stage.Node.Stage == PipelineStage.Verify).MenuItems.Select(item => item.Label),
            Does.Contain($"Start {Verifier} again"));
    }

    [Test]
    public void TheShellsReason_NamesTheVerifierAlreadyMade_AndAVerifierStandsUntilItReportsOrIsArchived()
    {
        PieceCard Verifier(SessionLifecycle lifecycle, bool hasRun = false, bool archived = false) =>
            Card("v", lifecycle, role: WorkstreamRole.Verifier) with { HasRun = hasRun, IsArchived = archived };
        var waiting = NewLink(LinkKind.ReportsBack, "v", "build");
        var reported = waiting with { LatestReport = new LinkReport(DateTimeOffset.UnixEpoch, "s", "r.md", "x: pass", "done") };

        Assert.That(VerifierCards.WhyAlreadyChecked(["Verify: Theseus"]),
            Is.EqualTo("Verify: Theseus is already made to check it, so no second is made"));
        Assert.That(new[]
            {
                VerifierCards.Stands(Verifier(SessionLifecycle.NotStarted), waiting),
                VerifierCards.Stands(Verifier(SessionLifecycle.Running, hasRun: true), waiting),
                VerifierCards.Stands(Verifier(SessionLifecycle.Ended, hasRun: true), waiting),
                VerifierCards.Stands(Verifier(SessionLifecycle.Ghost, hasRun: true), waiting),
                VerifierCards.Stands(Verifier(SessionLifecycle.Ended, hasRun: true), reported),
                VerifierCards.Stands(Verifier(SessionLifecycle.NotStarted, archived: true), waiting),
                VerifierCards.Stands(Card("v", SessionLifecycle.NotStarted), waiting),
            },
            Is.EqualTo(new[] { true, true, true, true, false, false, false }));
    }

    [Test]
    public void BySubject_FollowsEachVerifiersLatestActiveReportBack()
    {
        var verifier = Card("v", role: WorkstreamRole.Verifier);
        IReadOnlyList<Link> links =
        [
            NewLink(LinkKind.ReportsBack, "v", "old"),
            NewLink(LinkKind.ReportsBack, "v", "new"),
            NewLink(LinkKind.HandsOver, "v", "elsewhere"),
        ];
        var bySubject = VerifierCards.BySubject([verifier, Card("plain")], links);

        Assert.That(bySubject.Keys, Is.EqualTo(new[] { "new" }));
        Assert.That(bySubject["new"].Single().Key, Is.EqualTo("v"));
    }

    [Test]
    public void AReportedCheck_WaitsForYou_OnTheBoxAndInWaitsForYou()
    {
        // Review of P1, blocker 2: once a verifier reported it no longer stood, and the box read "has not run yet" with
        // Theseus to act, while the report waited for the owner and was counted nowhere.
        var view = WithVerifier(SessionLifecycle.Ended, hasRun: true, reported: true);
        var status = view.AllNodes.Single(node => node.Name == _build).Piece.Status;
        var lines = Of(view, WaitKind.ConfirmReport).Where(wait => wait.Item.Pieces.Contains(_build)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(status.WaitsForYou, Is.True);
            Assert.That(status.Text, Does.EndWith("has reported."));
            Assert.That(status.WhoActs, Does.StartWith("You: read the report"));
            Assert.That(lines, Has.Count.EqualTo(1), "one line, not one from the box and one from the link");
            Assert.That(lines[0].Item.CardKey, Is.EqualTo(Verifier));
            Assert.That(view.Overview.Asking.Select(wait => wait.Text), Has.Some.Contains("reported on"), "the badge counts it");
        });
    }

    [Test]
    public void AConfirmedCheck_NoLongerWaits()
    {
        var view = WithVerifier(SessionLifecycle.Ended, hasRun: true, reported: true);
        var confirmed = _views.BuildYourMarket(
            cards: [.. BuildYourMarketBoard(), Card(Verifier, SessionLifecycle.Ended, role: WorkstreamRole.Verifier) with { HasRun = true }],
            links: [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, _buildCard) with
            {
                LatestReport = new LinkReport(DateTimeOffset.UnixEpoch, "s", "r.md", "x: pass", "done"), SatisfiedUtc = DateTimeOffset.UnixEpoch,
            }]);

        Assert.That(view.AllNodes.Single(node => node.Name == _build).Piece.CheckReported, Is.Not.Null);
        Assert.That(confirmed.AllNodes.Single(node => node.Name == _build).Piece.CheckReported, Is.Null);
    }
}
