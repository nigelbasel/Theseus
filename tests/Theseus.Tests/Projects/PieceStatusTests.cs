using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>The status sentence of a piece of work, the first that applies (1.1d AC 7), checked word for word.</summary>
[TestFixture]
public class PieceStatusTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static PieceStatus StatusOf(IEnumerable<PieceOfWork> pieces, string name) => pieces.Single(piece => piece.Name == name).Status;

    [TestCase("project-view", "Waiting on you: accept the spec")]
    [TestCase("readme-runtime", "Next: Review")]
    [TestCase("resumed-stop", "Done")]
    [TestCase("method-kit", "Done")]
    public void OnTheTheseusFixture(string piece, string text)
    {
        Assert.That(StatusOf(TheseusPieces(_folder), piece).Text, Is.EqualTo(text));
    }

    [TestCase("bym-mcp-identity", "Verify: holdouts not run yet")]
    [TestCase(RedirectAnchoring, "Not started: no records yet")]
    [TestCase("bym-mcp-preconditions", "Freeform: done when objective 1.1 is done")]
    [TestCase(ServerSpec, "Done")]
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
    public void ABugsRule_IsAcceptedAsARule()
    {
        var piece = OnePiece(_folder, "bug", new Dictionary<string, string>
        {
            ["reproduction.md"] = "# Reproduction\n",
            ["fix.md"] = "# Fix\n\n## Spec\nThe rule.\n",
        });

        Assert.That(piece.Status.Text, Is.EqualTo("Waiting on you: accept the rule"));
    }

    [Test]
    public void AChangedSpec_IsAcceptedAgain()
    {
        var reading = WriteOne(_folder, "feature", Files("spec.md"));
        var stale = new SpecAcceptance(OneProject, APiece, "an-older-hash", DateTimeOffset.UnixEpoch);

        Assert.That(PiecesOfWork.Build(reading, _ => stale, [Card(ACard)], []).Single().Status.Text,
            Is.EqualTo("Waiting on you: accept the spec again"));
    }

    [Test]
    public void ASessionAsking_IsAnsweredInItsTerminal()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(Git22, SessionLifecycle.NeedsAttention) with { Key = ACard },
            accepted: true);

        Assert.That(piece.Status.Text, Is.EqualTo("Waiting on you: answer git-22 in its terminal"));
    }

    [Test]
    public void FailedEvidence_IsStuckAtVerify()
    {
        var records = Files("spec.md");
        records["evidence.md"] = FailedEvidence;

        Assert.That(OnePiece(_folder, "feature", records, accepted: true).Status,
            Is.EqualTo(new PieceStatus(StageNodeState.Stuck, "Stuck at Verify: its evidence failed")));
    }

    [Test]
    public void MissingEvidence_IsStuckAtVerify()
    {
        Assert.That(OnePiece(_folder, "feature", Files("spec.md", "review.md"), accepted: true).Status.Text,
            Is.EqualTo("Stuck at Verify: no evidence, though later records are written"));
    }

    [Test]
    public void ABlockedCard_IsStuck()
    {
        var piece = OnePiece(_folder, "feature", Files("spec.md"), Card(Git22, blocked: true) with { Key = ACard }, accepted: true);

        Assert.That(piece.Status.Text, Is.EqualTo("Stuck: git-22 is blocked"));
    }

    [Test]
    public void AWorkingSession_IsBusyOnItsStage()
    {
        var piece = OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md"),
            Card(Git22, SessionLifecycle.Running) with { Key = ACard }, accepted: true);

        Assert.That(piece.Status, Is.EqualTo(new PieceStatus(StageNodeState.Busy, "Busy: git-22 working on Implement")));
    }

    [Test]
    public void WaitingOnYou_ComesBeforeBusy()
    {
        var current = TheseusPieces(_folder, SessionLifecycle.Running).Single(piece => piece.Name == "projects-and-method-ui");

        Assert.That(current.Status.Text, Is.EqualTo("Waiting on you: accept the spec"));
    }

    [Test]
    public void EveryStageToDo_IsNotStarted()
    {
        Assert.That(OnePiece(_folder, "feature").Status, Is.EqualTo(new PieceStatus(StageNodeState.ToDo, "Not started: no records yet")));
    }

    [Test]
    public void FreeformWithNoRecords_IsNotStarted()
    {
        Assert.That(OnePiece(_folder, "freeform").Status.Text, Is.EqualTo("Not started: no records yet"));
    }

    [Test]
    public void OtherwiseItNamesTheNextStage_AfterTheLastDoneOne()
    {
        Assert.That(OnePiece(_folder, "feature", Files("research.md", "spec.md", "plan.md"), accepted: true).Status.Text,
            Is.EqualTo("Next: Implement"));
    }
}
