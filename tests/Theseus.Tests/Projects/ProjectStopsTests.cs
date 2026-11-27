using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.Tests.Projects;

/// <summary>
/// A stopped project in the settings (project-view-as-tabs AC 10, 11): its overnight queue goes off and comes back on
/// Start only if it was on, and the cards whose locks are still to release survive a restart until each is released.
/// </summary>
[TestFixture]
public class ProjectStopsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 21, 0, 0, TimeSpan.Zero);

    [Test]
    public void Stop_SwitchesTheQueueOff_AndRemembersItWasOn()
    {
        var settings = TheseusSettings.Default with { Duties = TheseusSettings.Default.Duties with { QueueProjects = ["bug-fixing"] } };

        var stopped = ProjectStops.Stop(settings, "bug-fixing", new ProjectStopping(Nine) { PendingLockCards = ["a"] });

        Assert.Multiple(() =>
        {
            Assert.That(stopped.Duties.QueueIsOn("bug-fixing"), Is.False);
            Assert.That(ProjectStops.Of(stopped, "Bug-Fixing")!.QueueWasOn, Is.True);
            Assert.That(ProjectStops.Of(stopped, "bug-fixing")!.PendingLockCards, Is.EqualTo(new[] { "a" }));
        });
    }

    [Test]
    public void Resume_PutsTheQueueBackOnlyIfItWasOn_AndReturnsWhatTheStopPaused()
    {
        var settings = ProjectStops.Stop(TheseusSettings.Default with { Duties = TheseusSettings.Default.Duties with { QueueProjects = ["theseus"] } }, "theseus",
            new ProjectStopping(Nine) { PausedDuties = ["d1"] });

        var (resumed, stopping) = ProjectStops.Resume(settings, "theseus");

        Assert.Multiple(() =>
        {
            Assert.That(resumed.Duties.QueueIsOn("theseus"), Is.True);
            Assert.That(ProjectStops.Of(resumed, "theseus"), Is.Null);
            Assert.That(stopping!.PausedDuties, Is.EqualTo(new[] { "d1" }));
        });
    }

    [Test]
    public void Resume_OfAProjectWhoseQueueWasOff_LeavesItOff()
    {
        var settings = ProjectStops.Stop(TheseusSettings.Default, "theseus", new ProjectStopping(Nine));

        var (resumed, _) = ProjectStops.Resume(settings, "theseus");

        Assert.That(resumed.Duties.QueueIsOn("theseus"), Is.False);
    }

    [Test]
    public void Resume_OfAProjectNotStopped_ChangesNothing()
    {
        var settings = TheseusSettings.Default;

        var (resumed, stopping) = ProjectStops.Resume(settings, "theseus");

        Assert.Multiple(() =>
        {
            Assert.That(resumed, Is.SameAs(settings));
            Assert.That(stopping, Is.Null);
        });
    }

    [Test]
    public void Released_TakesCardsOffThePendingList_AndKeepsTheStop()
    {
        var settings = ProjectStops.Stop(TheseusSettings.Default, "theseus", new ProjectStopping(Nine) { PendingLockCards = ["a", "b"] });

        var after = ProjectStops.Released(settings, "theseus", ["A"]);

        Assert.That(ProjectStops.Of(after, "theseus")!.PendingLockCards, Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public void Resume_WhileLocksAreStillToRelease_KeepsThemPending_ButTheProjectIsNoLongerStopped()
    {
        var settings = ProjectStops.Stop(TheseusSettings.Default, "theseus", new ProjectStopping(Nine) { PendingLockCards = ["a"], PausedDuties = ["d1"] });

        var (resumed, stopping) = ProjectStops.Resume(settings, "theseus");
        var (again, nothing) = ProjectStops.Resume(resumed, "theseus");

        Assert.Multiple(() =>
        {
            Assert.That(stopping!.PausedDuties, Is.EqualTo(new[] { "d1" }));
            Assert.That(ProjectStops.IsStopped(resumed, "theseus"), Is.False);
            Assert.That(ProjectStops.Of(resumed, "theseus")!.PendingLockCards, Is.EqualTo(new[] { "a" }));
            Assert.That(nothing, Is.Null, "a second Start resumes nothing");
            Assert.That(again, Is.SameAs(resumed));
        });
    }

    [Test]
    public void Released_OfTheLastPendingCardOfAResumedProject_ForgetsTheStop()
    {
        var settings = ProjectStops.Resume(
            ProjectStops.Stop(TheseusSettings.Default, "theseus", new ProjectStopping(Nine) { PendingLockCards = ["a"] }), "theseus").Settings;

        var after = ProjectStops.Released(settings, "theseus", ["a"]);

        Assert.That(ProjectStops.Of(after, "theseus"), Is.Null);
    }

    [Test]
    public void IsStopped_IsTrueFromStopUntilStart()
    {
        var settings = ProjectStops.Stop(TheseusSettings.Default, "theseus", new ProjectStopping(Nine));

        Assert.Multiple(() =>
        {
            Assert.That(ProjectStops.IsStopped(TheseusSettings.Default, "theseus"), Is.False);
            Assert.That(ProjectStops.IsStopped(settings, "Theseus"), Is.True);
        });
    }
}
