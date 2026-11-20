using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>What each kind of stage offers (1.1d AC 2), and Verify with holdouts on an older piece (code fact 4).</summary>
[TestFixture]
public class StageNodeActionsTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static StageAction[] Actions(StageNode stage) => [.. stage.Actions.Select(action => action.Action)];

    private static PieceOfWork Named(IEnumerable<PieceOfWork> pieces, string name) => pieces.Single(piece => piece.Name == name);

    [Test]
    public void ASpecWaitingOnYou_OffersOpenAndAcceptSpec_ThenOpenRecordsAndItsFile()
    {
        var spec = Named(TheseusPieces(_folder), "project-view").Stages[1];

        Assert.That(spec.Actions.Select(action => action.Label),
            Is.EqualTo(new[] { "Open spec", "Accept spec…", "Open records", "Open spec.md" }));
    }

    [Test]
    public void AnAcceptedSpec_OffersOnlyItsRecords()
    {
        var spec = OnePiece(_folder, "feature", Files("spec.md"), accepted: true).Stages[1];

        Assert.That(Actions(spec), Is.EqualTo(new[] { StageAction.OpenRecords, StageAction.OpenRecord }));
    }

    [Test]
    public void ASessionWaitingOnTheSpecStage_DoesNotOfferAcceptSpec()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md"), Card(ACard, SessionLifecycle.NeedsAttention));

        Assert.That(piece.Stages[1].State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(Actions(piece.Stages[1]), Does.Not.Contain(StageAction.AcceptSpec));
    }

    [Test]
    public void TheWorkStage_OfItsCardsCurrentPiece_OffersStart_WithNoSession()
    {
        var implement = Named(TheseusPieces(_folder, SessionLifecycle.Ended), "projects-and-method-ui").Stages[3];

        Assert.That(Actions(implement), Is.EqualTo(new[] { StageAction.Start, StageAction.OpenRecords, StageAction.OpenRecord }));
        Assert.That(implement.Actions[0].Label, Is.EqualTo("▶ Start"));
    }

    [Test]
    public void TheWorkStage_OfItsCardsCurrentPiece_OffersFocusAndStop_WithASession_NeverStart()
    {
        var implement = Named(TheseusPieces(_folder, SessionLifecycle.Idle), "projects-and-method-ui").Stages[3];

        Assert.That(implement.Actions.Select(action => action.Label).Take(2), Is.EqualTo(new[] { "⧉ Focus git-22", "■ Stop" }));
        Assert.That(Actions(implement), Does.Not.Contain(StageAction.Start));
    }

    [Test]
    public void TheWorkStage_OfAnOlderPiece_OffersNoSessionActions()
    {
        var implement = Named(TheseusPieces(_folder, SessionLifecycle.Idle), "project-view").Stages[3];

        Assert.That(Actions(implement), Is.EqualTo(new[] { StageAction.OpenRecords, StageAction.OpenRecord }));
    }

    [Test]
    public void FreeformWork_OffersTheSessionActions()
    {
        var work = OnePiece(_folder, "freeform", Files("plan.md")).Stages[0];

        Assert.That(Actions(work), Is.EqualTo(new[] { StageAction.Start, StageAction.OpenRecords }));
    }

    [Test]
    public void VerifyWithHoldoutsNotRun_OnTheCurrentPiece_IsOffered()
    {
        var verify = Named(TheseusPieces(_folder), "projects-and-method-ui").Stages[4];

        Assert.That(verify.Actions[0], Is.EqualTo(new StageActionOffer(StageAction.VerifyWithHoldouts, "Verify with holdouts…")));
    }

    [Test]
    public void VerifyWithHoldouts_OnAPieceItsCardHasLeft_IsDisabled_SayingSo()
    {
        var verify = Named(TheseusPieces(_folder), "project-view").Stages[4];

        Assert.That(verify.Actions[0], Is.EqualTo(new StageActionOffer(StageAction.VerifyWithHoldouts, "Verify with holdouts…", false,
            "git-22 has moved on to projects-and-method-ui; Verify with holdouts checks a card's current work")));
    }

    [Test]
    public void AVerifyStageDone_OffersNoVerifyWithHoldouts()
    {
        var verify = OnePiece(_folder, "feature", Files("spec.md", "evidence.md"), accepted: true).Stages[4];

        Assert.That(Actions(verify), Is.EqualTo(new[] { StageAction.OpenRecords, StageAction.OpenRecord }));
    }

    [Test]
    public void ARecordStage_OffersOpenRecords()
    {
        var research = Named(TheseusPieces(_folder), "project-view").Stages[0];

        Assert.That(research.Actions.Select(action => action.Label), Is.EqualTo(new[] { "Open records", "Open research.md" }));
    }

    [Test]
    public void WithNoRecordsFolder_OpenRecordsIsDisabled_SayingSo()
    {
        var work = Named(TheseusPieces(_folder), "method-kit").Stages[0];

        Assert.That(work.Actions.Single(action => action.Action == StageAction.OpenRecords),
            Is.EqualTo(new StageActionOffer(StageAction.OpenRecords, "Open records", false, "No records folder in the project yet")));
    }
}
