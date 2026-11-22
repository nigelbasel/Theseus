using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// A piece of work's state line, who acts next and its action, the first that applies (1.1f F31, replacing 1.1d AC 7),
/// checked word for word.
/// </summary>
[TestFixture]
public class PieceStatusTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static PieceStatus StatusOf(IEnumerable<PieceOfWork> pieces, string name) => pieces.Single(piece => piece.Name == name).Status;

    [TestCase("project-view", "Waiting for you: accept the spec.")]
    [TestCase("readme-runtime", "Next: Review.")]
    [TestCase("resumed-stop", "Done, before method-v0.2.")]
    [TestCase("method-kit", "Done, before method-v0.2.")]
    public void OnTheTheseusFixture(string piece, string text)
    {
        Assert.That(StatusOf(TheseusPieces(_folder), piece).Text, Is.EqualTo(text));
    }

    [TestCase("bym-mcp-identity", "Built, not yet checked by an independent test.")]
    [TestCase(RedirectAnchoring, "Not started; it is not listed in project.md.")]
    [TestCase("bym-mcp-preconditions", "Reported back; you confirmed it.")]
    [TestCase(ServerSpec, "Done: handed on to 7 pieces of work.")]
    public void OnTheBuildYourMarketFixture(string piece, string text)
    {
        Assert.That(StatusOf(BuildYourMarketPieces(_folder), piece).Text, Is.EqualTo(text));
    }

    [Test]
    public void TheStatesFollowTheSentences()
    {
        var pieces = TheseusPieces(_folder);

        Assert.That(StatusOf(pieces, "project-view").State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(StatusOf(pieces, "readme-runtime").State, Is.EqualTo(StageNodeState.ToDo));
        Assert.That(StatusOf(pieces, "resumed-stop").State, Is.EqualTo(StageNodeState.Done));
        Assert.That(StatusOf(pieces, "project-view").Mark, Is.EqualTo("◆"));
    }

    [Test]
    public void EveryStatus_HasItsMeaning_FromItsState()
    {
        var status = StatusOf(TheseusPieces(_folder), "project-view");

        Assert.That(status.Meaning, Is.EqualTo(StageNodeStates.Meaning(StageNodeState.WaitingOnYou)));
        Assert.That(status.Word, Is.EqualTo("Waiting for you"));
    }

    [Test]
    public void AGate_WaitsForYou_AndOffersAccept()
    {
        var status = StatusOf(TheseusPieces(_folder), "project-view");

        Assert.That(status.WhoActs, Is.EqualTo("You; nothing moves until you do."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.Accept));
        Assert.That(status.WaitsForYou, Is.True);
    }

    [Test]
    public void ABugsRule_IsAcceptedAsARule()
    {
        var piece = OnePiece(_folder, "bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        Assert.That(piece.Status.Text, Is.EqualTo("Waiting for you: accept the rule."));
    }

    [Test]
    public void AChangedSpec_IsAcceptedAgain()
    {
        var reading = WriteOne(_folder, "feature", Files("spec.md"));
        var stale = new SpecAcceptance(OneProject, APiece, "an-older-hash", DateTimeOffset.UnixEpoch);

        Assert.That(PiecesOfWork.Build(reading, _ => stale, [Card(ACard)], []).Single().Status.Text,
            Is.EqualTo("Waiting for you: accept the spec again."));
    }

    [Test]
    public void ASessionAsking_WaitsForYou_ToAnswerIt()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(Git22, SessionLifecycle.NeedsAttention) with { Key = ACard },
            accepted: true);

        Assert.That(piece.Status.Text, Is.EqualTo("Waiting for you: git-22 has asked you something in its terminal."));
        Assert.That(piece.Status.WhoActs, Is.EqualTo("You; it does nothing until you answer."));
        Assert.That(piece.Status.Action, Is.EqualTo(PieceAction.Answer));
        Assert.That(piece.Status.WaitsForYou, Is.True);
    }

    [Test]
    public void FailedEvidence_IsStuckAtVerify_AndOffersTheEvidence()
    {
        var records = Files("spec.md");
        records["evidence.md"] = FailedEvidence;
        var status = OnePiece(_folder, "feature", records, accepted: true).Status;

        Assert.That(status.Text, Is.EqualTo("Stuck at Verify: its evidence failed."));
        Assert.That(status.WhoActs, Is.EqualTo("a-card; nothing for you unless it asks."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.OpenEvidence));
        Assert.That(status.WaitsForYou, Is.False);
    }

    [Test]
    public void MissingEvidence_IsStuckAtVerify()
    {
        Assert.That(OnePiece(_folder, "feature", Files("spec.md", "review.md"), accepted: true).Status.Text,
            Is.EqualTo("Stuck at Verify: no evidence, though later records are written."));
    }

    [Test]
    public void ABlockedCard_IsStuck_AndWaitsForYou()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(Git22, blocked: true) with { Key = ACard }, accepted: true);

        Assert.That(piece.Status.Text, Is.EqualTo("Stuck: git-22 is blocked."));
        Assert.That(piece.Status.WhoActs, Is.EqualTo("You: it is marked blocked."));
        Assert.That(piece.Status.WaitsForYou, Is.True);
    }

    [Test]
    public void AWorkingSession_IsBusyOnWhatItSaid()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md"),
            Card(Git22, SessionLifecycle.Running) with { Key = ACard }, accepted: true,
            reports: Said(ACard, PipelineStage.Implement, "writing the reader"));

        Assert.That(piece.Status.Text, Is.EqualTo("Busy: writing the reader."));
        Assert.That(piece.Status.WhoActs, Is.EqualTo("git-22; no action needed."));
        Assert.That(piece.Status.Action, Is.EqualTo(PieceAction.Focus));
    }

    [Test]
    public void AWorkingSession_ThatHasNotSaid_IsBusyButHasNotSaidOnWhat()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md"),
            Card(Git22, SessionLifecycle.Running) with { Key = ACard }, accepted: true);

        Assert.That(piece.Status.Text, Is.EqualTo("Busy, but has not said on what."));
        Assert.That(piece.Stages, Has.None.Matches<StageNode>(stage => stage.State == StageNodeState.Busy));
    }

    [Test]
    public void ABuildBusyAheadOfItsSpec_SaysSo_AndWaitsForYourAcceptance()
    {
        var current = TheseusPieces(_folder, SessionLifecycle.Running).Single(piece => piece.Name == "projects-and-method-ui");

        Assert.That(current.Status.Text, Is.EqualTo("Busy building, ahead of your acceptance of its spec (at its own risk)."));
        Assert.That(current.Status.Action, Is.EqualTo(PieceAction.Accept));
        Assert.That(current.Status.WaitsForYou, Is.True);
    }

    [Test]
    public void BuiltNotChecked_WaitsForYouToStartTheCheck()
    {
        var status = StatusOf(BuildYourMarketPieces(_folder), "bym-mcp-identity");

        Assert.That(status.WhoActs, Is.EqualTo("Waiting for you: nothing happens until you start the check."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.StartCheck));
        Assert.That(status.WaitsForYou, Is.True);
        Assert.That(status.Meaning, Does.Contain("hidden from the builder (holdout scenarios)"));
    }

    [Test]
    public void BuiltNotChecked_WithItsSessionBusyAndSilent_SaysToCheckAfterItStops()
    {
        var cards = BuildYourMarketBoard().Select(card =>
            card.Key == "bym-mcp-w4-mcp-host-and-staff-plugin" ? card with { Lifecycle = SessionLifecycle.Running } : card).ToList();
        var host = BuildYourMarketPieces(_folder, cards).Single(piece => piece.Name == "bym-mcp-host");

        Assert.That(host.Stages[4].State, Is.EqualTo(StageNodeState.HoldoutsNotRun));
        Assert.That(host.Status.WhoActs, Is.EqualTo(
            "Waiting for you to start the check, but its session is busy and has not said on what: check after it stops"));
    }

    [Test]
    public void AnAnsweredPiece_SaysTheParentSessionTicksItsCriteria()
    {
        var status = StatusOf(BuildYourMarketPieces(_folder), "bym-mcp-preconditions");

        Assert.That(status.State, Is.EqualTo(StageNodeState.Answered));
        Assert.That(status.WhoActs, Is.EqualTo(
            "The Specify the BuildYourMarket MCP server session ticks objective 1.1's criteria; nothing for you."));
        Assert.That(status.WaitsForYou, Is.False);
    }

    [Test]
    public void APieceNotInTheProject_IsForYouToAddToAnObjective()
    {
        var status = StatusOf(BuildYourMarketPieces(_folder), RedirectAnchoring);

        Assert.That(status.State, Is.EqualTo(StageNodeState.NotInProject));
        Assert.That(status.WhoActs, Is.EqualTo("Nobody yet. Add it to an objective, or start it."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.AddToObjective));
    }

    [Test]
    public void EveryStageToDo_IsNotStarted_AndStartable()
    {
        var status = OnePiece(_folder, "feature").Status;

        Assert.That(status.State, Is.EqualTo(StageNodeState.ToDo));
        Assert.That(status.Text, Is.EqualTo("Not started: no records yet."));
        Assert.That(status.WhoActs, Is.EqualTo("a-card, once you start it."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.Start));
    }

    [Test]
    public void FreeformWithNoRecords_IsNotStarted()
    {
        Assert.That(OnePiece(_folder, "freeform").Status.Text, Is.EqualTo("Not started: no records yet."));
    }

    [Test]
    public void OtherwiseItNamesTheNextStage_AfterTheLastDoneOne()
    {
        Assert.That(OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md"), accepted: true).Status.Text,
            Is.EqualTo("Next: Implement."));
    }

    [Test]
    public void ADonePiece_NeedsNothing_AndOpensItsRecords()
    {
        var status = StatusOf(TheseusPieces(_folder), "stop-timing");

        Assert.That(status.State, Is.EqualTo(StageNodeState.Done));
        Assert.That(status.WhoActs, Is.EqualTo("No action needed."));
        Assert.That(status.Action, Is.EqualTo(PieceAction.OpenRecords));
    }
}
