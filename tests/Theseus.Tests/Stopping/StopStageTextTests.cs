using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// Where a stop has got to, as the card's stopping icon says it (close-step spec AC 5, 6 and 9). The words are the
/// spec's, checked against it and not against the code (P17).
/// </summary>
[TestFixture]
public class StopStageTextTests
{
    private static readonly DateTimeOffset Asked = new(2026, 11, 19, 13, 4, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Test]
    public void NoStop_IsNoStage()
    {
        Assert.That(StopState.Of(null, null, null, null), Is.Null);
    }

    [Test]
    public void AWaitingRequest_IsAskedAndWaitingForItsTurnToEnd()
    {
        Assert.That(Text(StopState.Of(Request(), null, null, null)), Is.EqualTo("Asked at 13:04. Waiting for its turn to end."));
    }

    [Test]
    public void ADeliveredRequest_IsHandedOverAndWritingItsHandoff()
    {
        var delivered = Request() with { DeliveredUtc = Asked.AddMinutes(2) };

        Assert.That(Text(StopState.Of(null, delivered, null, null)), Is.EqualTo("Handed over at 13:06. Writing its handoff."));
    }

    [Test]
    public void AHandedOverRequestWithNoDeliveryTime_UsesWhenItWasAsked()
    {
        Assert.That(Text(StopState.Of(null, Request(), null, null)), Is.EqualTo("Handed over at 13:04. Writing its handoff."));
    }

    [Test]
    public void AHandoffWrittenSinceTheRequest_ClosesOnceQuiet()
    {
        var delivered = Request() with { DeliveredUtc = Asked.AddMinutes(2) };

        Assert.That(Text(StopState.Of(null, delivered, Asked.AddMinutes(3), null)),
            Is.EqualTo("Handoff written at 13:07. Closes once it has been quiet for 20 s."));
    }

    [Test]
    public void AHandoffFromBeforeTheRequest_DoesNotCount()
    {
        var delivered = Request() with { DeliveredUtc = Asked.AddMinutes(2) };

        Assert.That(StopState.Of(null, delivered, Asked.AddDays(-1), null)!.Stage, Is.EqualTo(StopStage.HandedOver));
    }

    [Test]
    public void ASentExit_IsClosing()
    {
        var state = StopState.Of(null, Request(), Asked.AddMinutes(3), new ExitAttempt(ExitResult.Sent, Asked.AddMinutes(4)));

        Assert.That(Text(state), Is.EqualTo("Typed /exit at 13:08. Closing."));
    }

    [TestCase(ExitResult.TabNotConfirmed, "Could not type /exit: its tab could not be confirmed. Close now tries again.")]
    [TestCase(ExitResult.FocusLost, "Could not type /exit: the terminal was not in front. Close now tries again.")]
    [TestCase(ExitResult.Refused, "Could not type /exit: Windows refused the keystrokes. Close it yourself.")]
    [TestCase(ExitResult.Elevated, "Could not type /exit: the terminal runs as administrator and Theseus does not. Close it yourself.")]
    public void AFailedExit_SaysWhyAndWhatToDo(ExitResult result, string expected)
    {
        var state = StopState.Of(null, Request(), Asked.AddMinutes(3), new ExitAttempt(result, Asked.AddMinutes(4)));

        Assert.That(state!.Stage, Is.EqualTo(StopStage.ExitFailed));
        Assert.That(Text(state), Is.EqualTo(expected));
    }

    [Test]
    public void Times_AreShownInTheMachinesOwnZone()
    {
        var twoHoursAhead = TimeZoneInfo.CreateCustomTimeZone("plus-two", TimeSpan.FromHours(2), "plus-two", "plus-two");

        Assert.That(StopStageText.For(StopState.Of(Request(), null, null, null)!, twoHoursAhead),
            Is.EqualTo("Asked at 15:04. Waiting for its turn to end."));
    }

    private static string Text(StopState? state) => StopStageText.For(state!, Utc);

    private static PendingStopRequest Request() => new("d616ea10-e23d-4b77-8157-c39be7095633", "stop-check",
        "stop check", "Please stop.", Asked, Asked.AddHours(4));
}
