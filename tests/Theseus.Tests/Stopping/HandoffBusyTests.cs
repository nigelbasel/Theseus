using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>A card being started counts as running, though nothing has registered yet (handoff-names AC 3, review M1).</summary>
[TestFixture]
public class HandoffBusyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 22, 0, 0, TimeSpan.Zero);

    private static SessionRun Run(string key, DateTimeOffset started, SessionExitReason reason) =>
        new(Guid.NewGuid(), "session", key, @"C:\work", null, SessionLaunchMode.FirstRun, "prompt", "tab", "#000000",
            started, reason is SessionExitReason.Unknown ? null : started.AddMinutes(1), null, null, null, reason);

    [Test]
    public void ALiveCard_IsBusy()
    {
        Assert.That(HandoffBusy.Of("card", isLive: true, [], Now), Is.True);
    }

    [Test]
    public void ARunThatHasNotEnded_IsBusy_ThoughNothingIsLive()
    {
        Assert.That(HandoffBusy.Of("card", false, [Run("card", Now.AddHours(-1), SessionExitReason.Unknown)], Now), Is.True);
    }

    [Test]
    public void ALaunchWithinTheWindow_IsBusy_EvenIfItsRunReadsAsEnded()
    {
        Assert.That(HandoffBusy.Of("card", false, [Run("card", Now.AddSeconds(-30), SessionExitReason.ClosedCleanly)], Now),
            Is.True);
    }

    [Test]
    public void AnEndedRunFromEarlier_IsNotBusy()
    {
        Assert.That(HandoffBusy.Of("card", false, [Run("card", Now.AddHours(-1), SessionExitReason.ClosedCleanly)], Now),
            Is.False);
    }

    [Test]
    public void AnotherCardsRun_DoesNotCount()
    {
        Assert.That(HandoffBusy.Of("card", false, [Run("other", Now, SessionExitReason.Unknown)], Now), Is.False);
    }
}
