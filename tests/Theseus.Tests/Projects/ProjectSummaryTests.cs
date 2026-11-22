using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>Where the whole project is, in counts and one sentence (1.1d AC 8 to 10), checked word for word.</summary>
[TestFixture]
public class ProjectSummaryTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheTheseusFixture_WithGit22Idle()
    {
        Assert.That(ProjectSummary.For(TheseusPieces(_folder)).Sentence, Is.EqualTo(
            "12 pieces of work across 5 objectives: 7 done, 3 waiting on you, 2 still to do or not started. "
            + "git-22 is idle on projects-and-method-ui. Next: you accept the spec of projects-and-method-ui."));
    }

    [Test]
    public void TheBuildYourMarketFixture_WithNoSession()
    {
        Assert.That(ProjectSummary.For(BuildYourMarketPieces(_folder)).Sentence, Is.EqualTo(
            "9 pieces of work across 5 objectives: 1 done, 5 waiting for their holdout check, 1 answered, criteria not ticked, "
            + "2 not in project.md. "
            + "No session is working on it right now. Next: run the holdout scenarios."));
    }

    [TestCase(SessionLifecycle.Running, "git-22 is busy on projects-and-method-ui.")]
    [TestCase(SessionLifecycle.Launching, "git-22 is busy on projects-and-method-ui.")]
    [TestCase(SessionLifecycle.NeedsAttention, "git-22 is waiting on you on projects-and-method-ui.")]
    public void ALiveSession_IsNamedWithWhatItIsDoing(SessionLifecycle lifecycle, string words)
    {
        Assert.That(ProjectSummary.For(TheseusPieces(_folder, lifecycle)).Sentence, Does.Contain(words));
    }

    [Test]
    public void SeveralLiveSessions_AreJoinedBySemicolons()
    {
        var cards = BuildYourMarketBoard()
            .Select(card => card.Key switch
            {
                ServerSpec => card with { Lifecycle = SessionLifecycle.Idle },
                ClientFixes => card with { Lifecycle = SessionLifecycle.Running },
                _ => card,
            })
            .ToList();

        Assert.That(ProjectSummary.For(BuildYourMarketPieces(_folder, cards)).Sentence, Does.Contain(
            $"{ServerSpec} is idle on {ServerSpec}; {ClientFixes} is busy on {ClientFixes}."));
    }

    [Test]
    public void BusyAndStuck_AreCountedInTheirOrder()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(ACard, SessionLifecycle.Running), accepted: true);
        var stuck = OnePiece(_folder, "feature", Files("spec.md", "review.md"), accepted: true);

        Assert.That(ProjectSummary.For([piece, stuck]).Sentence,
            Is.EqualTo("2 pieces of work across 1 objective: 1 busy, 1 stuck. a-card is busy on a-piece."));
    }

    [Test]
    public void ABugWaitingOnYou_IsNextForItsRule()
    {
        var piece = OnePiece(_folder, "bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        Assert.That(ProjectSummary.For([piece]).Sentence, Does.EndWith(" Next: you accept the rule of a-piece."));
    }

    [Test]
    public void ASessionAsking_IsNextBeforeAnySpec()
    {
        Assert.That(ProjectSummary.For(TheseusPieces(_folder, SessionLifecycle.NeedsAttention)).Sentence,
            Does.EndWith("git-22 is waiting on you on projects-and-method-ui. Next: answer git-22 in its terminal."));
    }

    [Test]
    public void WithNothingWaitingAndNoHoldouts_ThereIsNoNext()
    {
        Assert.That(ProjectSummary.For([OnePiece(_folder, "feature")]).Sentence,
            Is.EqualTo("1 piece of work across 1 objective: 1 still to do or not started. No session is working on it right now."));
    }

    [Test]
    public void TheCounts_HoldEveryState_AndAreThePiecesStatuses()
    {
        var pieces = TheseusPieces(_folder);
        var summary = ProjectSummary.For(pieces);

        Assert.That(summary.Counts.Keys, Is.EquivalentTo(Enum.GetValues<StageNodeState>()));
        Assert.That(summary.CountOf(StageNodeState.Done), Is.EqualTo(7));
        Assert.That(summary.CountOf(StageNodeState.WaitingOnYou), Is.EqualTo(3));
        Assert.That(summary.CountOf(StageNodeState.ToDo), Is.EqualTo(2));
        Assert.That(summary.CountOf(StageNodeState.Busy), Is.Zero);

        foreach (var state in Enum.GetValues<StageNodeState>())
        {
            Assert.That(summary.CountOf(state), Is.EqualTo(pieces.Count(piece => piece.Status.State == state)), state.ToString());
        }
    }
}
