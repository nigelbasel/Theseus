using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// What a project's Start starts (1.1e AC 9): a stopped project, exactly its run state, skipping and naming the cards that
/// cannot start now; a project never stopped, the card of its first piece of work not done, else of its first row. Stop
/// asks each card with a session open.
/// </summary>
[TestFixture]
public class ProjectStartTests
{
    private const string Host = "bym-mcp-w4-mcp-host-and-staff-plugin";
    private const string Config = "bym-mcp-w2-config-p5-refusal";
    private const string Infra = "bym-mcp-w5-infra-hygiene-scripts-and-pipelines";

    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private IReadOnlyList<PieceOfWork> BuildYourMarketWith(Func<PieceCard, PieceCard> change) =>
        BuildYourMarketPieces(_folder, [.. BuildYourMarketBoard().Select(change)]);

    [Test]
    public void AStoppedProject_StartsExactlyItsRunState()
    {
        var start = ProjectStart.For("BuildYourMarket MCP server", BuildYourMarketWith(card => card), [Host, Config]);

        Assert.That(start.Keys, Is.EqualTo(new[] { Host, Config }));
        Assert.That((start.IsStopped, start.CanStart, start.Skipped), Is.EqualTo((true, true, Array.Empty<string>())));
        Assert.That(start.ToolTip, Does.StartWith("Start BuildYourMarket MCP server as it was when stopped: "));
    }

    [Test]
    public void ACardInTheRunState_ThatCannotStartNow_IsSkippedAndNamed()
    {
        var pieces = BuildYourMarketWith(card => card.Key switch
        {
            Host => card with { Lifecycle = SessionLifecycle.Running },
            Infra => card with { CanStart = false },
            _ => card,
        });

        var start = ProjectStart.For("BuildYourMarket MCP server", pieces, [Host, Infra, "gone-card", Config]);

        Assert.That(start.Keys, Is.EqualTo(new[] { Config }));
        Assert.That(start.Skipped, Has.Count.EqualTo(3));
        Assert.That(start.Skipped, Has.Some.EndsWith("is already running"));
        Assert.That(start.Skipped, Has.Some.EndsWith("cannot be started from the board"));
        Assert.That(start.Skipped, Has.Some.EqualTo("gone-card is no longer on the board"));
    }

    [Test]
    public void AProjectNeverStopped_StartsTheCardOfItsFirstPieceNotDone()
    {
        var pieces = BuildYourMarketWith(card => card);
        var expected = pieces.First(piece => piece.IsListed && piece.Card is not null && piece.Status.State != StageNodeState.Done);

        var start = ProjectStart.For("BuildYourMarket MCP server", pieces);

        Assert.That((start.IsStopped, start.Keys), Is.EqualTo((false, new[] { expected.CardKey })));
    }

    [Test]
    public void AProjectWithEveryPieceDone_StartsItsFirstRowsCard()
    {
        var done = OnePiece(_folder, "freeform", objectiveDone: true);

        Assert.That(ProjectStart.For("One", [done]).Keys, Is.EqualTo(new[] { ACard }));
    }

    [Test]
    public void AProjectNeverStopped_DoesNotStartACardTheOwnerStopped_AndSaysWhy()
    {
        var pieces = TheseusPieces(_folder, SessionLifecycle.Ended);

        var start = ProjectStart.For("Theseus bootstrap", pieces, null, [Git22]);

        Assert.That(start.CanStart, Is.False);
        Assert.That(start.ToolTip, Is.EqualTo("No card of Theseus bootstrap can start: git-22 was stopped by you"));
    }

    [Test]
    public void AProjectWhoseFirstCardRuns_CannotStart_AndStopAsksIt()
    {
        var pieces = TheseusPieces(_folder, SessionLifecycle.Running);

        Assert.That(ProjectStart.For("Theseus bootstrap", pieces).ToolTip,
            Is.EqualTo("No card of Theseus bootstrap can start: git-22 is already running"));
        Assert.That(ProjectStop.For("Theseus bootstrap", pieces).Keys, Is.EqualTo(new[] { Git22 }));
    }
}
