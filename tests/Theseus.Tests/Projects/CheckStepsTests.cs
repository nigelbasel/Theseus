using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// What Theseus does itself for a project's builds waiting for their independent check (git-22, 2026-11-23: nothing
/// Theseus makes waits on the owner's press): a verifier for each build a scenario names, one scenario writer when some
/// build is named by none and no writer stands, and a line saying what is under way, which asks nothing of him.
/// </summary>
[TestFixture]
public class CheckStepsTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private IReadOnlyList<PieceOfWork> Waiting() =>
        [.. BymPieces(_folder).Where(piece => piece.Status.State == StageNodeState.HoldoutsNotRun && piece.IsCurrentPiece)];

    [Test]
    public void ABuildAScenarioNames_GetsItsVerifier_WithNoPress()
    {
        var waiting = Waiting();
        var named = waiting.Where(piece => piece.Card is { IsWorking: false }).Take(2).Select(piece => piece.Name).ToHashSet();

        var plan = CheckSteps.For(BymPieces(_folder), piece => named.Contains(piece.Name), writerStands: true, writerRunning: false, canWrite: true);

        Assert.That(plan.VerifyKeys, Is.EquivalentTo(waiting.Where(piece => named.Contains(piece.Name)).Select(piece => piece.CardKey)));
        Assert.That(plan.MakeWriter, Is.False, "a writer stands already");
    }

    [Test]
    public void ABuildAlreadyChecked_GetsNoSecondVerifier_AndNeedsNoWriter()
    {
        var first = Waiting().First(piece => piece.Card is { IsWorking: false });

        var plan = CheckSteps.For(BymPieces(_folder), _ => true, writerStands: true, writerRunning: false, canWrite: true,
            isChecked: piece => piece.Name == first.Name);
        var unnamed = CheckSteps.For(BymPieces(_folder), piece => piece.Name != first.Name, writerStands: false, writerRunning: false,
            canWrite: true, isChecked: piece => piece.Name == first.Name);

        Assert.That(plan.VerifyKeys, Has.None.EqualTo(first.CardKey),
            "review of the follow-on, M1: its verifier reported, and after a restart a second was made");
        Assert.That(unnamed.MakeWriter, Is.False, "a checked build needs no scenarios written for it");
    }

    [Test]
    public void ABuildTheOwnerHeld_GetsNoVerifier()
    {
        var held = Waiting().First(piece => piece.Card is { IsWorking: false });

        var plan = CheckSteps.For(BymPieces(_folder), _ => true, writerStands: true, writerRunning: false, canWrite: true,
            isHeld: piece => piece.Name == held.Name);

        Assert.That(plan.VerifyKeys, Has.None.EqualTo(held.CardKey), "review of the follow-on, S3: paused, blocked or stopped by him");
    }

    [Test]
    public void ABuildStillBusy_WaitsForItsSessionToStop()
    {
        var busy = Waiting().First(piece => piece.Card is { IsWorking: true });

        var plan = CheckSteps.For(BymPieces(_folder), _ => true, writerStands: true, writerRunning: false, canWrite: true);

        Assert.That(plan.VerifyKeys, Has.None.EqualTo(busy.CardKey), "a build is checked once its own session has stopped");
    }

    [Test]
    public void BuildsNoScenarioNames_GetOneWriter_OnlyWhenNoneStands_AndTheFolderCanBeWritten()
    {
        Assert.That(CheckSteps.For(BymPieces(_folder), _ => false, writerStands: false, writerRunning: false, canWrite: true).MakeWriter, Is.True);
        Assert.That(CheckSteps.For(BymPieces(_folder), _ => false, writerStands: true, writerRunning: false, canWrite: true).MakeWriter, Is.False,
            "one writer per project: one that finished without naming every build is started again, not made again");
        Assert.That(CheckSteps.For(BymPieces(_folder), _ => false, writerStands: false, writerRunning: false, canWrite: false).MakeWriter, Is.False,
            "a holdout folder that is missing is the owner's to set");
    }

    [Test]
    public void AWriterThatStandsButIsNotRunning_IsStarted_WhileBuildsAreNamedByNoScenario()
    {
        var plan = CheckSteps.For(BymPieces(_folder), _ => false, writerStands: true, writerRunning: false, canWrite: true, idleWriter: "write-bym");

        Assert.That(plan.StartWriter, Is.EqualTo("write-bym"), "git-22, 2026-11-23: BYM's writers stood, never started, waiting on nobody");
        Assert.That(plan.MakeWriter, Is.False);
        Assert.That(CheckSteps.For(BymPieces(_folder), _ => true, writerStands: true, writerRunning: false, canWrite: true, idleWriter: "write-bym")
            .StartWriter, Is.Null, "every build is named: nothing for it to write");
    }

    [Test]
    public void ABuildWithAStandingVerifier_IsLeftAlone()
    {
        var pieces = BymPieces(_folder);
        var first = Waiting()[0];
        IReadOnlyList<PieceOfWork> withVerifier =
            [.. pieces.Select(piece => piece.Name == first.Name ? piece with { PendingVerifiers = [ProjectViewFixtures.Card("verify", SessionLifecycle.Running, role: WorkstreamRole.Verifier)] } : piece)];

        var plan = CheckSteps.For(withVerifier, _ => true, writerStands: true, writerRunning: false, canWrite: true);

        Assert.That(plan.VerifyKeys, Has.None.EqualTo(first.CardKey));
        Assert.That(plan.Underway, Does.Contain("Checking one build…"), "it says what is happening, and asks nothing");
    }

    [Test]
    public void ARunningWriter_SaysHowManyBuildsItWritesFor()
    {
        var unnamed = Waiting().Count;

        var plan = CheckSteps.For(BymPieces(_folder), _ => false, writerStands: true, writerRunning: true, canWrite: true);

        Assert.That(plan.Underway, Does.Contain($"Writing scenarios for {(unnamed == 5 ? "five" : unnamed.ToString())} builds…"));
    }
}
