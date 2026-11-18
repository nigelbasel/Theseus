using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// resumed-stop review S1 and S2 (2026-11-18): trying to type /exit is not the same as a stop finishing, and a stop
/// belongs to the session it was handed to.
/// </summary>
[TestFixture]
public class StopFinishedTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Asked = new(2026, 11, 18, 15, 0, 0, TimeSpan.Zero);

    [Test]
    public void AStopWhoseExitWasOnlyAttempted_HasNotFinished()
    {
        var exits = new ExitsTyped();

        exits.Typed(Request());

        Assert.That(exits.AlreadyTyped(Request()), Is.True, "it is still never tried twice");
        Assert.That(exits.IsFinished(Request()), Is.False, "the keystrokes may not have landed");
    }

    [Test]
    public void AStopWhoseExitLanded_HasFinished()
    {
        var exits = new ExitsTyped();

        exits.Typed(Request());
        exits.Finished(Request());

        Assert.That(exits.IsFinished(Request()), Is.True);
    }

    [Test]
    public void AStopHandedToTheSessionRunningNow_BelongsToIt()
    {
        var started = Asked.AddMinutes(-30);

        Assert.That(SessionClosing.HandedBefore(Request() with { DeliveredUtc = Asked.AddSeconds(1) }, started), Is.False);
    }

    [Test]
    public void AStopAskedOfAnEarlierRunButHandedToThisOne_BelongsToThisOne()
    {
        var started = Asked.AddMinutes(10);

        Assert.That(SessionClosing.HandedBefore(Request() with { DeliveredUtc = Asked.AddMinutes(11) }, started), Is.False,
            "the hook handed it to the session resumed at 15:10; that session has been told");
    }

    [Test]
    public void AStopHandedToAnEarlierRun_DoesNotBelongToTheSessionRunningNow()
    {
        var started = Asked.AddMinutes(10);

        Assert.That(SessionClosing.HandedBefore(Request() with { DeliveredUtc = Asked.AddSeconds(1) }, started), Is.True);
    }

    [Test]
    public void AStopWithNoDeliveryTime_IsJudgedByWhenItWasAsked()
    {
        Assert.That(SessionClosing.HandedBefore(Request(), Asked.AddMinutes(10)), Is.True);
        Assert.That(SessionClosing.HandedBefore(Request(), Asked.AddMinutes(-10)), Is.False);
    }

    private static PendingStopRequest Request() =>
        new(SessionId, "contact-forms", "Contact forms", "Write a handoff and stop.", Asked, Asked.AddHours(4));
}
