using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

/// <summary>
/// One card started twice never opens a second session (git-22, 2026-11-23): a launch stays in flight from its ticket until
/// its run ends, which covers the time its tab waits at Claude Code's question about trusting the folder, when no session
/// is registered yet and the board's own check sees nothing running.
/// </summary>
[TestFixture]
public class LaunchesInFlightTests
{
    [Test]
    public void ALaunch_IsInFlight_UntilItsRunEnds()
    {
        var launches = new LaunchesInFlight();

        launches.Began("verify-close-step", "ticket-1");

        Assert.That(launches.IsInFlight("Verify-Close-Step"), Is.True, "while its tab waits, a second start is refused");

        launches.Ended("ticket-1");

        Assert.That(launches.IsInFlight("verify-close-step"), Is.False);
    }

    [Test]
    public void AnEndForAnotherTicket_LeavesTheLaunchInFlight()
    {
        var launches = new LaunchesInFlight();
        launches.Began("verify-close-step", "ticket-2");

        launches.Ended("ticket-1");
        launches.Ended("ticket-unknown");

        Assert.That(launches.IsInFlight("verify-close-step"), Is.True, "an old run's ending does not end the new one");
    }

    [Test]
    public void OtherCards_AreNotInFlight()
    {
        var launches = new LaunchesInFlight();
        launches.Began("verify-close-step", "ticket-1");

        Assert.That(launches.IsInFlight("close-step"), Is.False);
    }
}
