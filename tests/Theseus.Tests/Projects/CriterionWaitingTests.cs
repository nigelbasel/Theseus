using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>A waiting note's asks: one note may list several, each on its own whom (project-view-scale, 2026-11-24).</summary>
[TestFixture]
public class CriterionWaitingTests
{
    [Test]
    public void ANoteOfOneAsk_HasThatAsk()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: Owner names the reviewer")!;

        Assert.That(waiting.Asks, Is.EqualTo(new[] { new WaitingAsk(true, "Owner names the reviewer") }));
    }

    [Test]
    public void ANoteOfSeveralAsks_SplitsAtEachWhom_InOrder()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: A names B; work: the store (1.3); owner: the team: an answer; work: C")!;

        Assert.That(waiting.Asks, Is.EqualTo(new[]
        {
            new WaitingAsk(true, "A names B"), new WaitingAsk(false, "the store (1.3)"), new WaitingAsk(true, "the team: an answer"),
            new WaitingAsk(false, "C"),
        }));
        Assert.That(waiting.Line, Is.EqualTo("Waiting: owner: A names B; work: the store (1.3); owner: the team: an answer; work: C"),
            "the file's line is kept as written");
    }

    [Test]
    public void AWorkNoteWithAnOwnerAskLater_StillWaitsOnTheOwner()
    {
        var criterion = new ProjectCriterion("A criterion.", false, 1) { Waiting = CriterionWaiting.Parse("Waiting: work: built; owner: Owner decides") };

        Assert.That((criterion.WaitsOnOwner, criterion.Waiting!.OwnerAsks.Single().What), Is.EqualTo((true, "Owner decides")));
    }

    [Test]
    public void ASemicolonInsideAnAsk_DoesNotSplitIt()
    {
        var waiting = CriterionWaiting.Parse("Waiting: owner: figures; and the slice definition")!;

        Assert.That(waiting.Asks.Single().What, Is.EqualTo("figures; and the slice definition"));
    }
}
