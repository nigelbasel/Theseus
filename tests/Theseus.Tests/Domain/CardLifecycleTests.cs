using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class CardLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 18, 0, 0, TimeSpan.Zero);

    [Test]
    public void For_ShowsAWorkstreamWithALiveSessionAsRunning()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: true, [], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Running));
    }

    [Test]
    public void For_KeepsSayingAWorkstreamRanAfterItsSessionExits()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: false, [Run("tailhawk", Now.AddHours(-2))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Ended),
            "a session that exited is not a workstream that was never started");
    }

    [Test]
    public void For_ShowsAWorkstreamThatHasNotRunTodayAsNotStarted()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: false, [Run("tailhawk", Now.AddDays(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_ShowsAWorkstreamWithNoHistoryAtAllAsNotStarted()
    {
        Assert.That(CardLifecycle.For("tailhawk", "busy", isLive: false, [], Now).Lifecycle,
            Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_IgnoresOtherWorkstreamsRuns()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: false, [Run("theseus", Now.AddHours(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NotStarted));
    }

    [Test]
    public void For_MatchesTheWorkstreamKeyTheWayTheFileSystemDoes()
    {
        var state = CardLifecycle.For("TailHawk", "busy", isLive: false, [Run("tailhawk", Now.AddHours(-1))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Ended));
    }

    [Test]
    public void For_DatesTheEndingFromWhenTheSessionActuallyEnded()
    {
        var ended = Now.AddMinutes(-5);
        var run = Run("tailhawk", Now.AddHours(-2)) with { EndedUtc = ended };

        Assert.That(CardLifecycle.For("tailhawk", "busy", isLive: false, [run], Now).Since, Is.EqualTo(ended));
    }

    [Test]
    public void For_SaysNothingAboutWhenARunEndedIfNobodyRecordedIt()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: false, [Run("tailhawk", Now.AddHours(-2))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Ended));
        Assert.That(state.Since, Is.Null,
            "the start is not the ending, and a card that reports one as the other is lying");
    }

    [Test]
    public void For_TakesTheMostRecentOfSeveralRunsToday()
    {
        var ended = Now.AddMinutes(-9);
        IReadOnlyList<SessionRun> runs =
        [
            Run("tailhawk", Now.AddHours(-6)) with { EndedUtc = Now.AddHours(-5) },
            Run("tailhawk", Now.AddMinutes(-10)) with { EndedUtc = ended },
            Run("tailhawk", Now.AddHours(-3)) with { EndedUtc = Now.AddHours(-2) },
        ];

        Assert.That(CardLifecycle.For("tailhawk", "busy", isLive: false, runs, Now).Since, Is.EqualTo(ended));
    }

    [Test]
    public void For_LetsALiveSessionOutrankTheHistory()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: true, [Run("tailhawk", Now.AddHours(-2))], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.Running));
        Assert.That(state.Since, Is.Null);
    }

    private static SessionRun Run(string key, DateTimeOffset started) =>
        new(Guid.NewGuid(), "session-1", key, @"C:\work", null, SessionLaunchMode.FirstRun,
            "Do the thing.", key, "#208DD7", started, null, null, null, null, SessionExitReason.Unknown);

    [Test]
    public void For_ShowsALiveSessionWaitingForAReplyAsNeedingAttention()
    {
        var state = CardLifecycle.For("tailhawk", "waiting", isLive: true, [], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NeedsAttention));
    }

    [Test]
    public void For_IgnoresAReportedStatusOnceTheSessionIsGone()
    {
        var state = CardLifecycle.For("tailhawk", "busy", isLive: false, [], Now);

        Assert.That(state.Lifecycle, Is.EqualTo(SessionLifecycle.NotStarted));
    }
}
