using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// How long a stop took, and how far an unfinished one got. Every figure comes from a time recorded on the
/// machine; a missing time stays missing (stop-timing spec, 2026-11-18).
/// </summary>
[TestFixture]
public class StopTimingTests
{
    private static readonly DateTimeOffset Asked = new(2026, 11, 18, 13, 45, 0, TimeSpan.Zero);

    [Test]
    public void AFinishedStop_IsTimedFromRequestToDeliveryToHandoffToExit()
    {
        var request = Request() with { DeliveredUtc = Asked.AddSeconds(31) };

        var durations = StopTiming.Finished(request, Asked.AddSeconds(33.4), Asked.AddSeconds(54.2));

        Assert.That(durations.Total, Is.EqualTo(TimeSpan.FromSeconds(54.2)));
        Assert.That(durations.RequestToDelivery, Is.EqualTo(TimeSpan.FromSeconds(31)));
        Assert.That(durations.DeliveryToHandoff, Is.EqualTo(TimeSpan.FromSeconds(2.4)).Within(TimeSpan.FromMilliseconds(1)));
        Assert.That(durations.RequestToHandoff, Is.EqualTo(TimeSpan.FromSeconds(33.4)).Within(TimeSpan.FromMilliseconds(1)));
        Assert.That(durations.HandoffToExit, Is.EqualTo(TimeSpan.FromSeconds(20.8)).Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public void AFinishedStopWithNoDeliveryTime_LeavesTheDeliveryFiguresMissing_AndStillTimesTheRest()
    {
        var durations = StopTiming.Finished(Request(), Asked.AddSeconds(40), Asked.AddSeconds(61));

        Assert.That(durations.RequestToDelivery, Is.Null, "a hook older than the change records no delivery time");
        Assert.That(durations.DeliveryToHandoff, Is.Null);
        Assert.That(durations.RequestToHandoff, Is.EqualTo(TimeSpan.FromSeconds(40)));
        Assert.That(durations.HandoffToExit, Is.EqualTo(TimeSpan.FromSeconds(21)));
        Assert.That(durations.Total, Is.EqualTo(TimeSpan.FromSeconds(61)));
    }

    [Test]
    public void AFinishedStopWhoseHandoffTimeCannotBeRead_StillHasItsTotal()
    {
        var durations = StopTiming.Finished(Request(), null, Asked.AddSeconds(61));

        Assert.That(durations.Total, Is.EqualTo(TimeSpan.FromSeconds(61)));
        Assert.That(durations.RequestToHandoff, Is.Null);
        Assert.That(durations.HandoffToExit, Is.Null);
    }

    [Test]
    public void ARequestNeverDelivered_GotNoFurtherThanThat()
    {
        Assert.That(StopTiming.Progress(Request(), delivered: false, Asked.AddMinutes(1)), Is.EqualTo(StopProgress.NeverDelivered),
            "a handoff written while the request was still waiting is not an answer to it");
    }

    [Test]
    public void ADeliveredRequestWithNoHandoff_GotAsFarAsDelivery()
    {
        Assert.That(StopTiming.Progress(Request(), delivered: true, null), Is.EqualTo(StopProgress.DeliveredNoHandoff));
    }

    [Test]
    public void AHandoffWrittenBeforeTheRequest_IsNotThisStopsHandoff()
    {
        Assert.That(StopTiming.Progress(Request(), delivered: true, Asked.AddHours(-3)), Is.EqualTo(StopProgress.DeliveredNoHandoff));
    }

    [Test]
    public void AHandoffWrittenAtOrAfterTheRequest_MeansItHandedOverButWasNotClosed()
    {
        Assert.That(StopTiming.Progress(Request(), delivered: true, Asked), Is.EqualTo(StopProgress.HandedOverNotClosed),
            "the same rule as SessionClosing: written at the request counts");
        Assert.That(StopTiming.Progress(Request(), delivered: true, Asked.AddSeconds(30)), Is.EqualTo(StopProgress.HandedOverNotClosed));
    }

    [TestCase(StopProgress.NeverDelivered, "never delivered: the session did not end a turn")]
    [TestCase(StopProgress.DeliveredNoHandoff, "delivered, but no handoff was written")]
    [TestCase(StopProgress.HandedOverNotClosed, "handed over, but /exit was never typed")]
    public void HowFarAStopGot_IsSaidPlainly(StopProgress progress, string wording)
    {
        Assert.That(StopTiming.Describe(progress), Is.EqualTo(wording));
    }

    [Test]
    public void AFinishedStopWithEveryTime_SaysEachStep()
    {
        var text = StopTiming.FinishedText(new StopDurations(
            TimeSpan.FromSeconds(54.2), TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(2.4),
            TimeSpan.FromSeconds(33.4), TimeSpan.FromSeconds(20.8)));

        Assert.That(text, Is.EqualTo("finished in 54.2s: delivered after 31.0s, handoff 2.4s later, /exit 20.8s after that"));
    }

    [Test]
    public void AFinishedStopWithNoDeliveryTime_SaysSo()
    {
        var text = StopTiming.FinishedText(new StopDurations(
            TimeSpan.FromSeconds(61), null, null, TimeSpan.FromSeconds(40), TimeSpan.FromSeconds(21)));

        Assert.That(text, Is.EqualTo(
            "finished in 61.0s: delivery time not recorded, handoff 40.0s after the request, /exit 21.0s after that"));
    }

    [Test]
    public void AFinishedStopWhoseHandoffTimeCannotBeRead_DoesNotBlameTheDeliveryTime()
    {
        var text = StopTiming.FinishedText(new StopDurations(
            TimeSpan.FromSeconds(61), TimeSpan.FromSeconds(30), null, null, null));

        Assert.That(text, Is.EqualTo("finished in 61.0s: delivered after 30.0s, handoff time could not be read"));
    }

    [Test]
    public void AFinishedStopWithNeitherTime_StillGivesItsTotal()
    {
        Assert.That(StopTiming.FinishedText(new StopDurations(TimeSpan.FromSeconds(61), null, null, null, null)),
            Is.EqualTo("finished in 61.0s: delivery time not recorded, handoff time could not be read"));
    }

    [Test]
    public void ANegativeFigure_IsFlaggedRatherThanPassedOffAsATime()
    {
        var text = StopTiming.FinishedText(new StopDurations(
            TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(-5),
            TimeSpan.FromSeconds(25), TimeSpan.FromSeconds(25)));

        Assert.That(text, Does.EndWith("(a negative figure means the clock or a file time moved)"));
    }

    [TestCase(StopRemoval.StartedAgain, "its card was started again")]
    [TestCase(StopRemoval.NewerSession, "a newer session is running")]
    [TestCase(StopRemoval.Declined, "declined when the board opened")]
    [TestCase(StopRemoval.Expired, "it expired")]
    public void AnUnfinishedStop_SaysWhyItWasRemovedAndHowFarItGot(StopRemoval removal, string why)
    {
        Assert.That(StopTiming.UnfinishedText(removal, TimeSpan.FromHours(4), StopProgress.DeliveredNoHandoff),
            Is.EqualTo($"did not finish ({why}, 4.0h after it was asked): delivered, but no handoff was written"));
    }

    [TestCase(0.4, "0s")]
    [TestCase(45, "45s")]
    [TestCase(89, "89s")]
    [TestCase(3599, "59m")]
    [TestCase(5399, "89m")]
    [TestCase(14400, "4.0h")]
    public void AnAge_IsGivenInAUnitThatReadsNaturally(double seconds, string wording)
    {
        Assert.That(StopTiming.Age(TimeSpan.FromSeconds(seconds)), Is.EqualTo(wording));
    }

    private static PendingStopRequest Request() =>
        new("11111111-2222-3333-4444-555555555555", "contact-forms", "Contact forms", "Write a handoff and stop.",
            Asked, Asked.AddHours(4));
}
