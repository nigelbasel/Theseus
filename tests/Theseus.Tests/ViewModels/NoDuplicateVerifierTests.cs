using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A build that already has a verifier card, unstarted or running, gets no second one (Nigel, 2026-11-23: Start the
/// checks… stayed after it made the verifier cards, so a second press made a duplicate of each). Theseus starts a verifier
/// itself when it makes one (Nigel, 2026-11-23), so the Waits line names a verifier only when its start failed, with why
/// and Start again, which runs the card's own Start.
/// </summary>
[TestFixture]
public class NoDuplicateVerifierTests
{
    private const string Verifier = "verify-identity";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;
    private string _build = string.Empty;
    private string _buildCard = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        var checks = _views.BuildYourMarket().Waits.Single(wait => wait.Item.Kind == WaitKind.StartChecks);
        _build = checks.Item.Pieces[0];
        _buildCard = BuildYourMarketCards.Single(entry => entry.Piece == _build).Card;
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private const string WhyNot = "Windows Terminal opened the tab but never ran the session in it";

    private ProjectViewModel WithVerifier(SessionLifecycle lifecycle, bool archived = false, bool hasRun = false, string? failed = null)
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            Card(Verifier, lifecycle, role: WorkstreamRole.Verifier) with { IsArchived = archived, HasRun = hasRun, StartFailure = failed },
        ];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, _buildCard)];

        return _views.BuildYourMarket(cards: cards, links: links);
    }

    private static IReadOnlyList<WaitItemViewModel> Of(ProjectViewModel view, WaitKind kind) =>
        [.. view.Waits.Where(wait => wait.Item.Kind == kind)];

    [Test]
    public void AnUnstartedVerifier_TakesItsBuildOutOfStartTheChecks()
    {
        var checks = Of(WithVerifier(SessionLifecycle.NotStarted), WaitKind.StartChecks).Single();

        Assert.That(checks.Item.Pieces, Has.None.EqualTo(_build));
        Assert.That(checks.Item.Pieces, Has.Count.EqualTo(4));
    }

    [Test]
    public void AVerifierBeingStarted_NeedsNothingFromYou()
    {
        var view = WithVerifier(SessionLifecycle.NotStarted);

        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty, "Theseus starts it (Nigel, 2026-11-23): no \"only you start it\"");
        Assert.That(view.AllNodes.Single(node => node.Name == _build).Actions.Select(action => action.Label),
            Has.None.Contains(Verifier), "no ▶ Start on its build's box");
    }

    [Test]
    public void AVerifierWhoseStartFailed_SaysWhyInTheWaitsLine_WithStartAgain_WhichRunsTheCardsStart()
    {
        var line = Of(WithVerifier(SessionLifecycle.NotStarted, failed: WhyNot), WaitKind.StartVerifier).Single();

        Assert.That(line.Button, Is.EqualTo("Start again"));
        Assert.That(line.Text, Is.EqualTo($"{Verifier} did not start: {WhyNot}."));

        line.Command!.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { $"Start:{Verifier}" }));
    }

    [Test]
    public void ARunningVerifier_NeedsNothingFromYou_AndItsBuildIsNotOfferedAgain()
    {
        var view = WithVerifier(SessionLifecycle.Running);

        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty);
        Assert.That(Of(view, WaitKind.StartChecks).Single().Item.Pieces, Has.None.EqualTo(_build));
    }

    [Test]
    public void EveryBuildWithAVerifier_LeavesNoStartTheChecksButton()
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            .. BuildYourMarketCards.Select(entry => Card($"verify-{entry.Card}", role: WorkstreamRole.Verifier)),
        ];
        IReadOnlyList<Link> links =
        [
            .. BuildYourMarketLinks(),
            .. BuildYourMarketCards.Select(entry => NewLink(LinkKind.ReportsBack, $"verify-{entry.Card}", entry.Card)),
        ];
        var view = _views.BuildYourMarket(cards: cards, links: links);

        Assert.That(Of(view, WaitKind.StartChecks), Is.Empty);
        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty, "each is started by Theseus as it is made");
    }

    [Test]
    public void AFinishedOrArchivedVerifier_LetsTheBuildBeCheckedAgain()
    {
        Assert.That(Of(WithVerifier(SessionLifecycle.Ended), WaitKind.StartChecks).Single().Item.Pieces, Does.Contain(_build));
        Assert.That(Of(WithVerifier(SessionLifecycle.NotStarted, archived: true), WaitKind.StartChecks).Single().Item.Pieces,
            Does.Contain(_build));
    }

    [Test]
    public void AVerifierWhoseStartFailed_HasStartAgain_OnItsBuildsBox_AndItsVerifyStage()
    {
        var box = WithVerifier(SessionLifecycle.NotStarted, failed: WhyNot).AllNodes.Single(node => node.Name == _build);
        var start = box.Actions.Single(action => action.Label == $"Start {Verifier} again");
        var inMenu = box.Stages.Single(stage => stage.Node.Stage == PipelineStage.Verify).MenuItems.Single(item => item.Label == start.Label);

        Assert.That((start.IsEnabled, inMenu.IsEnabled), Is.EqualTo((true, true)));

        start.Command.Execute(null);
        inMenu.Command.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { $"Start:{Verifier}", $"Start:{Verifier}" }));
    }

    [Test]
    public void ARunningVerifier_OffersNoStart_OnItsBuildsBox()
    {
        var box = WithVerifier(SessionLifecycle.Running).AllNodes.Single(node => node.Name == _build);

        Assert.That(box.Actions.Select(action => action.Label), Has.None.Contains(Verifier));
    }

    // The live case (git-22, 2026-11-23): "Verify: Theseus" checks a piece that is Done, not awaiting its check.
    [Test]
    public void AFailedVerifier_OfAPieceThatIsDone_IsStillInTheWaitsLine_AndOnItsVerifyStage()
    {
        var done = _views.BuildYourMarket().AllNodes.First(node => node.Piece is { IsCurrentPiece: true, Card: not null }
                                                                   && node.Piece.Status.State == StageNodeState.Done
                                                                   && node.Piece.Stages.Any(stage => stage.Stage == PipelineStage.Verify));
        IReadOnlyList<PieceCard> cards = [.. BuildYourMarketBoard(), Card(Verifier, role: WorkstreamRole.Verifier) with { StartFailure = WhyNot }];
        IReadOnlyList<Link> links = [.. BuildYourMarketLinks(), NewLink(LinkKind.ReportsBack, Verifier, done.Piece.CardKey)];
        var view = _views.BuildYourMarket(cards: cards, links: links);
        var box = view.AllNodes.Single(node => node.Name == done.Name);

        Assert.That(box.Piece.Status.State, Is.EqualTo(StageNodeState.Done));
        Assert.That(Of(view, WaitKind.StartVerifier).Single().Item.CardKey, Is.EqualTo(Verifier));
        Assert.That(box.Stages.Single(stage => stage.Node.Stage == PipelineStage.Verify).MenuItems.Select(item => item.Label),
            Does.Contain($"Start {Verifier} again"));
    }

    // NotStarted means "no run today" (CardLifecycle), so a verifier that ran yesterday reads NotStarted this morning
    // (review, finding 1): it no longer stands, and its build can be checked again.
    [Test]
    public void AVerifierThatRanBefore_AndHasNoSessionNow_NoLongerStands()
    {
        var view = WithVerifier(SessionLifecycle.NotStarted, hasRun: true);

        Assert.That(Of(view, WaitKind.StartChecks).Single().Item.Pieces, Does.Contain(_build));
        Assert.That(Of(view, WaitKind.StartVerifier), Is.Empty);
    }

    [Test]
    public void TheShellsReason_NamesTheVerifierAlreadyMade_AndOnlyAStandingVerifierCounts()
    {
        PieceCard Verifier(SessionLifecycle lifecycle, bool hasRun = false, bool archived = false) =>
            Card("v", lifecycle, role: WorkstreamRole.Verifier) with { HasRun = hasRun, IsArchived = archived };

        Assert.That(VerifierCards.WhyAlreadyChecked(["Verify: Theseus"]),
            Is.EqualTo("Verify: Theseus is already made to check it, so no second is made"));
        Assert.That(new[]
            {
                VerifierCards.Stands(Verifier(SessionLifecycle.NotStarted)),
                VerifierCards.Stands(Verifier(SessionLifecycle.Running, hasRun: true)),
                VerifierCards.Stands(Verifier(SessionLifecycle.NeedsAttention, hasRun: true)),
                VerifierCards.Stands(Verifier(SessionLifecycle.NotStarted, hasRun: true)),
                VerifierCards.Stands(Verifier(SessionLifecycle.Ended, hasRun: true)),
                VerifierCards.Stands(Verifier(SessionLifecycle.NotStarted, archived: true)),
                VerifierCards.Stands(Card("v", SessionLifecycle.NotStarted)),
            },
            Is.EqualTo(new[] { true, true, true, false, false, false, false }));
        Assert.That(VerifierCards.Stands(Verifier(SessionLifecycle.Ghost, hasRun: true) with { StartFailure = "its tab never ran" }), Is.True,
            "a verifier whose start failed stands until it is started again, so its failure is shown and no second is made");
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
}
