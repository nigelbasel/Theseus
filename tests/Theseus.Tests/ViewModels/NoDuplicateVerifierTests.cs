using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A build that already has a verifier card, unstarted or running, gets no second one (Nigel, 2026-11-23: Start the
/// checks… stayed after it made the verifier cards, so a second press made a duplicate of each). The Waits line names
/// each unstarted verifier with its own ▶ Start, which runs the card's own Start; only the owner starts a verifier.
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

    private ProjectViewModel WithVerifier(SessionLifecycle lifecycle, bool archived = false)
    {
        IReadOnlyList<PieceCard> cards =
        [
            .. BuildYourMarketBoard(),
            Card(Verifier, lifecycle, role: WorkstreamRole.Verifier) with { IsArchived = archived },
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
    public void AnUnstartedVerifier_IsNamedInTheWaitsLine_WithItsOwnStart_WhichRunsTheCardsStart()
    {
        var line = Of(WithVerifier(SessionLifecycle.NotStarted), WaitKind.StartVerifier).Single();

        Assert.That(line.Button, Is.EqualTo("▶ Start"));
        Assert.That(line.Text, Does.Contain(Verifier));
        Assert.That(line.Text, Does.Contain("only you start it"));

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
        Assert.That(Of(view, WaitKind.StartVerifier), Has.Count.EqualTo(BuildYourMarketCards.Length), "one per verifier, whatever its subject's state");
    }

    [Test]
    public void AFinishedOrArchivedVerifier_LetsTheBuildBeCheckedAgain()
    {
        Assert.That(Of(WithVerifier(SessionLifecycle.Ended), WaitKind.StartChecks).Single().Item.Pieces, Does.Contain(_build));
        Assert.That(Of(WithVerifier(SessionLifecycle.NotStarted, archived: true), WaitKind.StartChecks).Single().Item.Pieces,
            Does.Contain(_build));
    }

    [Test]
    public void AnUnstartedVerifier_HasItsOwnStart_OnItsBuildsBox_AndItsVerifyStage()
    {
        var box = WithVerifier(SessionLifecycle.NotStarted).AllNodes.Single(node => node.Name == _build);
        var start = box.Actions.Single(action => action.Label == $"▶ Start {Verifier}");
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

        Assert.That(box.Actions.Select(action => action.Label), Has.None.EqualTo($"▶ Start {Verifier}"));
    }

    // The live case (git-22, 2026-11-23): "Verify: Theseus" checks a piece that is Done, not awaiting its check.
    [Test]
    public void AnUnstartedVerifier_OfAPieceThatIsDone_IsStillInTheWaitsLine_AndOnItsVerifyStage()
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
            Does.Contain($"▶ Start {Verifier}"));
    }

    [Test]
    public void TheShellsReason_NamesTheVerifierAlreadyMade()
    {
        Assert.That(VerifierCards.WhyAlreadyChecked(["Verify: Theseus"]),
            Is.EqualTo("Verify: Theseus is already created to check it: start it rather than make another"));
        Assert.That(VerifierCards.IsPending(SessionLifecycle.NotStarted), Is.True);
        Assert.That(VerifierCards.IsPending(SessionLifecycle.Running), Is.True);
        Assert.That(VerifierCards.IsPending(SessionLifecycle.Ended), Is.False);
    }
}
