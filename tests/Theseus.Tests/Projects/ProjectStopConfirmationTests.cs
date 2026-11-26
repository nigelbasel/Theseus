using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Stop the project's one confirmation (project-view-as-tabs AC 10): the cards asked to stop, the holds and queue places
/// released once they have, that a test run keeps its lock until it ends, and that nothing is closed.
/// </summary>
[TestFixture]
public class ProjectStopConfirmationTests
{
    [Test]
    public void For_NamesTheRunningCards_TheHoldsAndPlaces_AndSaysNothingIsClosed()
    {
        var stop = ProjectStopConfirmation.For("Bug Fixing",
            [new ProjectCloseCard("a", "Fix NUF-1", true, false), new ProjectCloseCard("b", "Fix NUF-2", true, false),
                new ProjectCloseCard("c", "Idle one", false, false)],
            duties: 1, queueOn: true, holds: 2, places: 1, runs: 0);

        Assert.Multiple(() =>
        {
            Assert.That(stop.Caption, Is.EqualTo("Stop Bug Fixing?"));
            Assert.That(stop.Action, Is.EqualTo("_Stop project"));
            Assert.That(stop.Question, Does.Contain("2 cards are running, Fix NUF-1, Fix NUF-2"));
            Assert.That(stop.Question, Does.Contain("1 duty pauses"));
            Assert.That(stop.Question, Does.Contain("overnight queue"));
            Assert.That(stop.Question, Does.Contain("2 lock holds and 1 place in a lock queue are released"));
            Assert.That(stop.Question, Does.Contain("Nothing is closed"));
            Assert.That(stop.Question, Does.Not.Contain("Idle one"));
        });
    }

    [Test]
    public void For_SaysATestRunKeepsItsLockUntilItEnds()
    {
        var stop = ProjectStopConfirmation.For("Theseus", [new ProjectCloseCard("a", "Build", true, false)], 0, false, holds: 0, places: 0, runs: 1);

        Assert.That(stop.Question, Does.Contain("1 test run keeps its lock until it ends"));
    }

    [Test]
    public void For_WithNoLocks_SaysNoneAreHeld()
    {
        var stop = ProjectStopConfirmation.For("Theseus", [], 0, false, 0, 0, 0);

        Assert.That(stop.Question, Does.Contain("No card of Theseus is running").And.Contain("hold no lock"));
    }
}
