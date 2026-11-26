using Theseus.Core.Breakers;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// When what keeps a project's built pieces from their independent check is the project or its scenario writer, not the
/// build, Waits for you says so in plain words with what clears it (Nigel, 2026-11-25: "1 busy ... and 9 not yet checked
/// ... how to I make the checks run"). Both projects' writers had tripped their breakers, and each build only said no
/// scenario named it.
/// </summary>
[TestFixture]
public class CheckHoldTests
{
    private static readonly IReadOnlyList<PieceOfWork> Waiting =
    [
        Piece("lead-search", "Lead search"),
        Piece("lead-notes", "Lead notes"),
    ];

    private static PieceOfWork Piece(string name, string title) =>
        new(name, "card-" + name, title, null, null, null, null, false, []) { Title = title };

    private static Workstream Writer(BreakerTrip? breaker = null) =>
        new("writer", "Write holdout scenarios for LeadPro", "#208DD7", false, @"C:\holdouts", [], "Write them.", 0, "New", false, false,
            false, "session-1", [], null, DateTimeOffset.UnixEpoch)
        {
            Role = WorkstreamRole.ScenarioWriter,
            Breaker = breaker,
            IsPaused = breaker is not null,
        };

    private static readonly BreakerTrip NoProgress = new(BreakerKind.NoProgress,
        "Breaker tripped: 3 runs in a row with no new commit, record, tick or report.", DateTimeOffset.UnixEpoch);

    private static WaitItem? For(bool projectStopped = false, Workstream? writer = null, bool writerHeld = false,
        Func<PieceOfWork, bool>? isNamed = null) =>
        CheckHold.For("LeadPro", projectStopped, Waiting, isNamed ?? (_ => false), writer, writerHeld);

    [Test]
    public void AWriterStoppedByItsBreaker_IsNamed_WithWhy_AndResetBreakerOnItsCard()
    {
        var wait = For(writer: Writer(NoProgress), writerHeld: true)!;

        Assert.Multiple(() =>
        {
            Assert.That(wait.Kind, Is.EqualTo(WaitKind.ResetBreaker));
            Assert.That(wait.CardKey, Is.EqualTo("writer"));
            Assert.That(wait.Pieces, Is.EqualTo(new[] { "lead-search", "lead-notes" }));
            Assert.That(wait.Text, Does.StartWith("2 built pieces cannot be checked, Lead search, Lead notes:"));
            Assert.That(wait.Text, Does.Contain("no holdout scenario names them"));
            Assert.That(wait.Text, Does.Contain("Write holdout scenarios for LeadPro, which writes them, is stopped by its breaker"));
            Assert.That(wait.Text, Does.Contain("3 runs in a row with no new commit, record, tick or report"));
            Assert.That(wait.Text, Does.EndWith("Reset its breaker and Theseus starts it again."));
        });
    }

    [Test]
    public void AWriterTheOwnerPaused_IsNamed_WithStartItFromItsCard()
    {
        var wait = For(writer: Writer(), writerHeld: true)!;

        Assert.That(wait.Kind, Is.EqualTo(WaitKind.CheckCannotRun));
        Assert.That(wait.Text, Does.Contain("Write holdout scenarios for LeadPro, which writes them, is paused").And.EndWith("Start it from its card."));
    }

    [Test]
    public void AStoppedProject_SaysTheseusStartsNoChecks_UntilItIsStarted_ForNamedBuildsToo()
    {
        var wait = For(projectStopped: true, writer: Writer(NoProgress), writerHeld: true, isNamed: _ => true)!;

        Assert.Multiple(() =>
        {
            Assert.That(wait.Kind, Is.EqualTo(WaitKind.CheckCannotRun));
            Assert.That(wait.Pieces, Has.Count.EqualTo(2));
            Assert.That(wait.Text, Does.Contain("LeadPro is stopped, so Theseus starts no checks"));
            Assert.That(wait.Text, Does.EndWith("Start the project with ▶ on its card."));
        });
    }

    [Test]
    public void OnlyTheBuildsNoScenarioNames_WaitForTheWriter()
    {
        var wait = For(writer: Writer(NoProgress), writerHeld: true, isNamed: piece => piece.Name == "lead-search")!;

        Assert.That(wait.Pieces, Is.EqualTo(new[] { "lead-notes" }));
        Assert.That(wait.Text, Does.StartWith("Lead notes cannot be checked:"));
        Assert.That(wait.Text, Does.Contain("no holdout scenario names it"));
    }

    [Test]
    public void AWriterThatIsFree_OrNoWriter_OrNothingWaiting_IsNotThisLine()
    {
        Assert.That(For(writer: Writer()), Is.Null, "a free writer is started by Theseus; the build's own reason covers the rest");
        Assert.That(For(), Is.Null);
        Assert.That(CheckHold.For("LeadPro", projectStopped: true, [], _ => false, null, false), Is.Null);
        Assert.That(For(writer: Writer(NoProgress), writerHeld: true, isNamed: _ => true), Is.Null, "every build is named");
    }
}
