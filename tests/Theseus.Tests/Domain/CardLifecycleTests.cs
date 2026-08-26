using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class CardLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_ShowsAWorkstreamWithALiveSessionAsRunning()
    {
        var state = CardLifecycle.For("tailhawk", isLive: true, [], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Running));
    }

    [Test]
    public void For_KeepsSayingAWorkstreamRanAfterItsSessionExits()
    {
        var state = CardLifecycle.For("tailhawk", isLive: false, [Run("tailhawk", Now.AddHours(-2))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Ended),
            "a session that exited is not a workstream that was never started");
    }

    [Test]
    public void For_ShowsAWorkstreamThatHasNotRunTodayAsNotStarted()
    {
        var state = CardLifecycle.For("tailhawk", isLive: false, [Run("tailhawk", Now.AddDays(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_ShowsAWorkstreamWithNoHistoryAtAllAsNotStarted()
    {
        Assert.That(CardLifecycle.For("tailhawk", isLive: false, [], Now).Lifecycle,
            Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_IgnoresOtherWorkstreamsRuns()
    {
        var state = CardLifecycle.For("tailhawk", isLive: false, [Run("theseus", Now.AddHours(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_MatchesTheWorkstreamKeyTheWayTheFileSystemDoes()
    {
        var state = CardLifecycle.For("TailHawk", isLive: false, [Run("tailhawk", Now.AddHours(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Ended));
    }

    [Test]
    public void For_DatesTheEndingFromWhenTheSessionActuallyEnded()
    {
        var ended = Now.AddMinutes(-5);
        var run = Run("tailhawk", Now.AddHours(-2)) with { EndedUtc = ended };

        Assert.That(CardLifecycle.For("tailhawk", isLive: false, [run], Now).Since, Is.EqualTo(ended));
    }

    [Test]
    public void For_FallsBackToTheStartWhenNobodyRecordedTheEnding()
    {
        var started = Now.AddHours(-2);

        Assert.That(CardLifecycle.For("tailhawk", isLive: false, [Run("tailhawk", started)], Now).Since,
            Is.EqualTo(started));
    }

    [Test]
    public void For_TakesTheMostRecentOfSeveralRunsToday()
    {
        var latest = Now.AddMinutes(-10);
        IReadOnlyList<SessionRun> runs =
            [Run("tailhawk", Now.AddHours(-6)), Run("tailhawk", latest), Run("tailhawk", Now.AddHours(-3))];

        Assert.That(CardLifecycle.For("tailhawk", isLive: false, runs, Now).Since, Is.EqualTo(latest));
    }

    [Test]
    public void For_LetsALiveSessionOutrankTheHistory()
    {
        var state = CardLifecycle.For("tailhawk", isLive: true, [Run("tailhawk", Now.AddHours(-2))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Running));
        Assert.That(state.Since, Is.Null);
    }

    private static SessionRun Run(string key, DateTimeOffset started) =>
        new(Guid.NewGuid(), "session-1", key, @"C:\work", null, SessionLaunchMode.FirstRun,
            "Do the thing.", key, "#208DD7", started, null, null, null, null, SessionExitReason.Unknown);
}
