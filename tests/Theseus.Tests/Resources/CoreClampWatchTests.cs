using Theseus.Core.Resources;
using Theseus.Core.Settings;

namespace Theseus.Tests.Resources;

/// <summary>
/// When the board warns that cores are being held back (parked-cores spec AC 3 and 4). The figures are the ones recorded
/// on a 20-processor laptop in November 2026: clamped under heat, idle, and in healthy heavy use after the fix.
/// </summary>
[TestFixture]
public class CoreClampWatchTests
{
    private static readonly CoreSample Clamp = new(20, 16, RunningBusyPercent: 98, ClockPercent: 120);
    private static readonly CoreSample Idle = new(20, 17, RunningBusyPercent: 8, ClockPercent: 100);
    private static readonly CoreSample HeavyHealthy = new(20, 7, RunningBusyPercent: 95, ClockPercent: 150);

    [Test]
    public void AClampHeldForThreeSamples_StartsTheWarning()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());

        watch.Observe(Clamp);
        watch.Observe(Clamp);
        var third = watch.Observe(Clamp);

        Assert.That(third.Standing, Is.True);
        Assert.That(third.Started, Is.True);
    }

    [Test]
    public void ABurstShorterThanTheHold_DoesNotWarn()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());

        watch.Observe(Clamp);
        var second = watch.Observe(Clamp);
        var after = watch.Observe(HeavyHealthy);

        Assert.That(second.Standing, Is.False);
        Assert.That(after.Standing, Is.False);
    }

    [Test]
    public void IdleWithMostCoresParked_NeverWarns()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());

        var states = Enumerable.Range(0, 10).Select(_ => watch.Observe(Idle)).ToList();

        Assert.That(states.Any(state => state.Standing), Is.False, "16 to 17 of 20 parked is how this machine idles");
    }

    [Test]
    public void HealthyHeavyUse_NeverWarns()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());

        var states = Enumerable.Range(0, 10).Select(_ => watch.Observe(HeavyHealthy)).ToList();

        Assert.That(states.Any(state => state.Standing), Is.False);
    }

    [Test]
    public void OneSampleWithoutAClamp_StopsTheWarning()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());
        watch.Observe(Clamp);
        watch.Observe(Clamp);
        watch.Observe(Clamp);
        var still = watch.Observe(Clamp);

        var stopped = watch.Observe(Idle);

        Assert.That(still.Started, Is.False, "it starts once, not on every sample");
        Assert.That(stopped.Standing, Is.False);
        Assert.That(stopped.Stopped, Is.True);
    }

    [Test]
    public void AnUnreadableSample_IsNoClamp_AndStopsTheWarning()
    {
        var watch = new CoreClampWatch(new CoreClampSettings());
        watch.Observe(Clamp);
        watch.Observe(Clamp);
        watch.Observe(Clamp);

        var unread = watch.Observe(null);

        Assert.That(unread.Standing, Is.False);
        Assert.That(unread.Stopped, Is.True);
    }

    [TestCase(8, 4, true)]
    [TestCase(8, 3, false)]
    [TestCase(64, 32, true)]
    [TestCase(64, 31, false)]
    public void TheRuleIsInShares_NotCounts(int total, int parked, bool clamp)
    {
        Assert.That(CoreClampWatch.ShowsClamp(new CoreSample(total, parked, 97, null), new CoreClampSettings()), Is.EqualTo(clamp));
    }

    [Test]
    public void RunningCoresBelowTheBusyLine_AreNoClamp()
    {
        Assert.That(CoreClampWatch.ShowsClamp(Clamp with { RunningBusyPercent = 89.9 }, new CoreClampSettings()), Is.False);
        Assert.That(CoreClampWatch.ShowsClamp(Clamp with { RunningBusyPercent = 90 }, new CoreClampSettings()), Is.True);
    }

    [Test]
    public void TheSettingsAreHonoured()
    {
        var strict = new CoreClampSettings { ParkedShare = 0.9, RunningBusyPercent = 99, HeldSamples = 1 };
        var watch = new CoreClampWatch(strict);

        Assert.That(watch.Observe(Clamp).Standing, Is.False, "16 of 20 is 80%, below the 90% asked for");
        Assert.That(watch.Observe(new CoreSample(20, 19, 99.5, null)).Standing, Is.True, "one sample is enough when the hold is one");
    }
}
