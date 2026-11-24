using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// A stop the session took and answered, after which it stayed open: seen live on 2026-11-24, when two sessions answered
/// their stops at 14:48 and 14:51, took another turn an hour later, and every Stop after that said it had already been
/// handed its stop.
/// </summary>
[TestFixture]
public class AnsweredStopTests
{
    private static readonly DateTimeOffset Asked = new(2026, 11, 24, 14, 51, 9, TimeSpan.Zero);
    private static readonly DateTimeOffset Delivered = Asked.AddSeconds(1);

    [Test]
    public void AnIdleSessionHandedItsStopLongerAgoThanTheAnswerWindow_IsAskedAfresh()
    {
        Assert.That(SessionClosing.IsSpent(Request(), "idle", Delivered + SessionClosing.AnswerWindow + TimeSpan.FromSeconds(1)),
            Is.True);
    }

    [Test]
    public void AnIdleSessionStillWithinTheAnswerWindow_IsLeftToTheCloseStep()
    {
        Assert.That(SessionClosing.IsSpent(Request(), "idle", Delivered + SessionClosing.AnswerWindow - TimeSpan.FromSeconds(1)),
            Is.False);
    }

    [Test]
    public void AWorkingSession_IsNeverAskedAfresh_ItMayStillBeWritingItsHandover()
    {
        Assert.That(SessionClosing.IsSpent(Request(), "busy", Delivered + TimeSpan.FromHours(1)), Is.False);
    }

    [Test]
    public void ARequestWithNoDeliveryTime_IsJudgedFromWhenItWasAsked()
    {
        PendingStopRequest asked = Request() with { DeliveredUtc = null };

        Assert.That(SessionClosing.IsSpent(asked, "idle", Asked + SessionClosing.AnswerWindow + TimeSpan.FromSeconds(1)), Is.True);
    }

    private static PendingStopRequest Request() =>
        new("429eb8e6-d92c-46f6-8004-284de4f4b986", "wb7", "WB7", "Please stop.", Asked, Asked.AddHours(4))
        {
            DeliveredUtc = Delivered,
        };
}
