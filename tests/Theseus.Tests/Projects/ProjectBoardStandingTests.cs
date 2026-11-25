using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>Where a project card sits on a derived axis: by its cards (1.1e AC 4, decision 1 to confirm).</summary>
[TestFixture]
public class ProjectBoardStandingTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [TestCase(SessionLifecycle.NeedsAttention, BoardView.NeedsYou)]
    [TestCase(SessionLifecycle.Running, BoardView.Working)]
    [TestCase(SessionLifecycle.Launching, BoardView.Working)]
    [TestCase(SessionLifecycle.Idle, BoardView.Idle)]
    [TestCase(SessionLifecycle.Ended, BoardView.NotRunning)]
    public void AProjectsColumn_IsItsRunningCardsState_ElseNotRunning(SessionLifecycle git22, string column)
    {
        Assert.That(ProjectBoardStanding.StateColumn(TheseusPieces(_folder, git22)), Is.EqualTo(column));
    }

    [Test]
    public void TheMostUrgentRunningCard_DecidesTheColumn()
    {
        var idle = OnePiece(_folder, "freeform", card: Card(ACard, SessionLifecycle.Idle)) with { IsCurrentPiece = true };
        var asking = idle with { Name = "other", Card = Card("other", SessionLifecycle.NeedsAttention) };
        var working = idle with { Name = "third", Card = Card("third", SessionLifecycle.Running) };

        Assert.That(ProjectBoardStanding.StateColumn([idle, working, asking]), Is.EqualTo(BoardView.NeedsYou));
        Assert.That(ProjectBoardStanding.StateColumn([idle, working]), Is.EqualTo(BoardView.Working));
    }

    [Test]
    public void AVerifierWaitingOnTheOwner_PutsItsProjectInNeedsYou_ThoughItWorksNoPiece()
    {
        var idle = OnePiece(_folder, "freeform", card: Card(ACard, SessionLifecycle.Idle)) with { IsCurrentPiece = true };
        var verifier = Card("verify-a", SessionLifecycle.NeedsAttention) with { Role = WorkstreamRole.Verifier };

        Assert.That(ProjectBoardStanding.StateColumn([idle], [verifier]), Is.EqualTo(BoardView.NeedsYou));
        Assert.That(ProjectBoardStanding.StateColumn([idle], [verifier with { Lifecycle = SessionLifecycle.Running }]), Is.EqualTo(BoardView.Idle),
            "a verifier at work moves nothing: only one waiting on the owner has nowhere else to show");
    }

    [Test]
    public void AProjectWithEveryPieceDone_AndNothingRunning_IsDone()
    {
        var done = OnePiece(_folder, "freeform", objectiveDone: true);

        Assert.That(done.Status.State, Is.EqualTo(StageNodeState.Done), "the fixture's piece is done");
        Assert.That(ProjectBoardStanding.StateColumn([done]), Is.EqualTo(BoardView.Done));
        Assert.That(ProjectBoardStanding.StateColumn([]), Is.EqualTo(BoardView.NotRunning), "nothing to be done is not done");
    }

    [Test]
    public void ItsFirstCard_IsTheFirstPieceInFileOrderWithACard()
    {
        var pieces = TheseusPieces(_folder);

        Assert.That(ProjectBoardStanding.FirstCardKey(pieces), Is.EqualTo(pieces.First(piece => piece.Card is not null).CardKey));
        Assert.That(ProjectBoardStanding.FirstCardKey([pieces[0] with { Card = null }]), Is.Null);
    }
}
