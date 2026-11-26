using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// A spec written is a spec waiting for the owner, whatever its objective's pipeline says (Nigel, 2026-11-26: "I dont see
/// any acccept spec notifications in the ui"). The 2.2 and 2.3 specs were written under objectives whose pipeline was
/// "[to be chosen]": their pieces ran freeform, drew no Spec stage and offered no Accept, while gates() read their spec.md
/// and said it waited.
/// </summary>
[TestFixture]
public class SpecWaitsWithNoPipelineTests
{
    private const string NoneChosen = "[to be chosen]";

    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static IReadOnlyList<WaitItem> WaitsOf(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces) =>
        ProjectOverview.For(reading.File!, pieces, [], [], 0).Waits;

    [TestCase(NoneChosen)]
    [TestCase("freeform")]
    public void ASpecWritten_UnderAnObjectiveWithNoGatedPipeline_WaitsForYourAccept(string pipeline)
    {
        var piece = OnePiece(_folder, pipeline, Files("spec.md"));

        Assert.Multiple(() =>
        {
            Assert.That(piece.Status.Action, Is.EqualTo(PieceAction.Accept));
            Assert.That(piece.Status.WaitsForYou, Is.True);
            Assert.That(piece.Stages.OfType<SpecStageNode>().Single().AwaitsAcceptance, Is.True);
        });
    }

    [TestCase(NoneChosen)]
    [TestCase("freeform")]
    public void ASpecWritten_UnderAnObjectiveWithNoGatedPipeline_IsALineOfWaitsForYou(string pipeline)
    {
        var reading = WriteOne(_folder, pipeline, Files("spec.md"));
        var pieces = PiecesOfWork.Build(reading, _ => null, [Card(ACard)], []);

        var line = WaitsOf(reading, pieces).Single(wait => wait.Pieces.Contains(APiece));

        Assert.Multiple(() =>
        {
            Assert.That(line.Kind, Is.EqualTo(WaitKind.AcceptSpec));
            Assert.That(line.Button, Is.EqualTo("Accept spec…"));
        });
    }

    [Test]
    public void TheGateTheBoardDraws_IsTheGateGatesReads()
    {
        // gates() and the box must never disagree on whether a spec waits.
        var reading = WriteOne(_folder, NoneChosen, Files("spec.md"));

        Assert.Multiple(() =>
        {
            Assert.That(SpecGates.SpecOf(reading, APiece), Is.Not.Null);
            Assert.That(StageCalculator.PipelineFor(reading.File!.ObjectiveOf(APiece)!.Pipelines, reading.Artefacts[APiece].Files),
                Is.EqualTo(PipelineKind.Feature));
        });
    }

    [Test]
    public void NoPipelineChosen_AndNoSpec_StaysFreeform()
    {
        var piece = OnePiece(_folder, NoneChosen, Files("notes.md"));

        Assert.That(piece.IsFreeform, Is.True);
    }

    [Test]
    public void NoPipelineChosen_AndABugsRecords_TakesTheBugPipeline()
    {
        var piece = OnePiece(_folder, NoneChosen, new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        Assert.Multiple(() =>
        {
            Assert.That(piece.Pipeline, Is.EqualTo(PipelineKind.Bug));
            Assert.That(piece.Status.Action, Is.EqualTo(PieceAction.Accept));
        });
    }

    [TestCase(NoneChosen)]
    [TestCase("feature")]
    public void AStoppedCardThatBuiltAhead_StillWaitsForYourAccept_InWaitsForYou(string pipeline)
    {
        var reading = WriteOne(_folder, pipeline, Files("spec.md", "plan.md", "implementation.md"));
        var pieces = PiecesOfWork.Build(reading, _ => null, [Card(ACard)], []);

        Assert.That(WaitsOf(reading, pieces).Select(wait => wait.Kind), Does.Contain(WaitKind.AcceptSpec));
    }

    [TestCase(NoneChosen)]
    [TestCase("feature")]
    public void ARunningCardThatBuiltAhead_StillWaitsForYourAccept_InWaitsForYou(string pipeline)
    {
        var reading = WriteOne(_folder, pipeline, Files("spec.md", "plan.md", "implementation.md"));
        var pieces = PiecesOfWork.Build(reading, _ => null, [Card(ACard, SessionLifecycle.Running)], []);

        Assert.That(WaitsOf(reading, pieces).Select(wait => wait.Kind), Does.Contain(WaitKind.AcceptSpec));
    }

    [Test]
    public void AnAcceptanceReportedForAnOlderVersion_IsNotConfirmed_TheSpecWaitsForYourAcceptAgain()
    {
        // 2.3 reported version b241816 in its tab; the spec then changed, and the board would have asked him to confirm
        // an acceptance of text no longer on disk.
        var reading = WriteOne(_folder, "feature", Files("spec.md"));
        var stale = new ReportedAcceptance(OneProject, APiece, "0000000older", ACard, ACard, "Accept", DateTimeOffset.UnixEpoch);

        var pieces = PiecesOfWork.WithReports(PiecesOfWork.Build(reading, _ => null, [Card(ACard)], []), reading, [stale]);

        Assert.Multiple(() =>
        {
            Assert.That(pieces.Single().Reported, Is.Null);
            Assert.That(WaitsOf(reading, pieces).Single().Kind, Is.EqualTo(WaitKind.AcceptSpec));
        });
    }

    [Test]
    public void AnAcceptanceReportedForTheVersionOnDisk_WaitsForYourConfirmation()
    {
        var reading = WriteOne(_folder, "feature", Files("spec.md"));
        var current = new ReportedAcceptance(OneProject, APiece, reading.Artefacts[APiece].SpecHash!, ACard, ACard, "Accept",
            DateTimeOffset.UnixEpoch);

        var pieces = PiecesOfWork.WithReports(PiecesOfWork.Build(reading, _ => null, [Card(ACard)], []), reading, [current]);

        Assert.That(WaitsOf(reading, pieces).Single().Kind, Is.EqualTo(WaitKind.ConfirmAcceptance));
    }
}
