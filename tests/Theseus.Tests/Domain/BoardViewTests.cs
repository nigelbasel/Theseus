using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Tests.Domain;

/// <summary>How the board's other views (Option E) name a card's column and row.</summary>
[TestFixture]
public class BoardViewTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [TestCase(SessionLifecycle.NeedsAttention, false, BoardView.NeedsYou)]
    [TestCase(SessionLifecycle.Running, false, BoardView.Working)]
    [TestCase(SessionLifecycle.Launching, true, BoardView.Working)]
    [TestCase(SessionLifecycle.Idle, true, BoardView.Idle)]
    [TestCase(SessionLifecycle.Ended, false, BoardView.NotRunning)]
    [TestCase(SessionLifecycle.NotStarted, true, BoardView.Done)]
    public void StateColumn_FollowsTheSessionThenTheStage(SessionLifecycle lifecycle, bool isInLastStage, string column)
    {
        Assert.That(BoardView.StateColumn(lifecycle, isInLastStage), Is.EqualTo(column));
    }

    [TestCase(@"C:\dev\git\Theseus", "Theseus")]
    [TestCase(@"C:\dev\tfsgit\features\gdpr\Nurtur.Direct.Communications\", "Nurtur.Direct.Communications")]
    [TestCase(@"C:\", @"C:")]
    [TestCase("  ", BoardView.NoProject)]
    public void Project_IsTheFolderItself(string directory, string project)
    {
        Assert.That(BoardView.Project(directory), Is.EqualTo(project));
    }

    [Test]
    public void ChainRoots_NamesEveryLinkedCardByTheCardItsChainStartsFrom()
    {
        var roots = ChainRoots.Of(
        [
            Link(LinkKind.HandsOver, "analysis", "design"),
            Link(LinkKind.HandsOver, "design", "build"),
            Link(LinkKind.ReportsBack, "build", "design"),
            Link(LinkKind.HandsOver, "spike", "spike-follow-on"),
            Link(LinkKind.HandsOver, "old", "gone") with { RemovedUtc = Nine },
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(roots["build"], Is.EqualTo("analysis"));
            Assert.That(roots["design"], Is.EqualTo("analysis"));
            Assert.That(roots["analysis"], Is.EqualTo("analysis"));
            Assert.That(roots["spike-follow-on"], Is.EqualTo("spike"));
            Assert.That(roots.ContainsKey("gone"), Is.False, "a removed link makes no chain");
        });
    }

    private static Link Link(LinkKind kind, string from, string to) => new($"lk-{from}-{to}", kind, from, to, Nine, Theseus.Core.Links.Link.MadeOnTheBoard);
}
