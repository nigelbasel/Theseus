using Theseus.Core.Abstractions;
using Theseus.Core.Usage;

namespace Theseus.Tests.Usage;

[TestFixture]
public class UsagePollScheduleTests
{
    private MovableClock _clock = null!;
    private UsagePollSchedule _schedule = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new MovableClock(new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero));
        _schedule = new UsagePollSchedule(_clock);
    }

    [Test]
    public void MayPollNow_AllowsTheFirstReadImmediately()
    {
        Assert.That(_schedule.MayPollNow, Is.True);
    }

    [Test]
    public void RecordSuccess_HoldsOffForTheMinimumInterval()
    {
        _schedule.RecordSuccess();

        Assert.That(_schedule.MayPollNow, Is.False);

        _clock.Advance(UsagePollSchedule.MinimumInterval - TimeSpan.FromSeconds(1));
        Assert.That(_schedule.MayPollNow, Is.False);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.That(_schedule.MayPollNow, Is.True);
    }

    [Test]
    public void RecordThrottled_WaitsFarLongerThanTheOrdinaryInterval()
    {
        _schedule.RecordThrottled();

        _clock.Advance(UsagePollSchedule.MinimumInterval);
        Assert.That(_schedule.MayPollNow, Is.False);

        _clock.Advance(UsagePollSchedule.FirstBackoff);
        Assert.That(_schedule.MayPollNow, Is.True);
    }

    [Test]
    public void RecordThrottled_DoublesTheWaitEachTimeTheServiceRefuses()
    {
        _schedule.RecordThrottled();
        var first = _schedule.NextAllowedUtc - _clock.UtcNow;

        _clock.Advance(first);
        _schedule.RecordThrottled();
        var second = _schedule.NextAllowedUtc - _clock.UtcNow;

        Assert.That(second, Is.EqualTo(first * 2));
    }

    [Test]
    public void RecordThrottled_StopsGrowingAtTheCeiling()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            _schedule.RecordThrottled();
            _clock.Advance(_schedule.NextAllowedUtc - _clock.UtcNow);
        }

        _schedule.RecordThrottled();

        Assert.That(_schedule.NextAllowedUtc - _clock.UtcNow, Is.EqualTo(UsagePollSchedule.MaximumBackoff));
    }

    [Test]
    public void RecordSuccess_ClearsAnAccumulatedBackoff()
    {
        _schedule.RecordThrottled();
        _schedule.RecordThrottled();
        _clock.Advance(TimeSpan.FromHours(1));

        _schedule.RecordSuccess();

        Assert.That(_schedule.NextAllowedUtc - _clock.UtcNow, Is.EqualTo(UsagePollSchedule.MinimumInterval));
    }

    [Test]
    public void RecordFailure_WaitsTheOrdinaryIntervalBecauseAnOfflineMachineIsNotARefusal()
    {
        _schedule.RecordFailure();

        Assert.That(_schedule.NextAllowedUtc - _clock.UtcNow, Is.EqualTo(UsagePollSchedule.MinimumInterval));
    }

    [Test]
    public void MinimumInterval_IsNoFasterThanTheEndpointTolerates()
    {
        Assert.That(UsagePollSchedule.MinimumInterval, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(180)));
    }

    private sealed class MovableClock : ISystemClock
    {
        public MovableClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
