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

    private static PendingStopRequest Request() =>
        new("11111111-2222-3333-4444-555555555555", "contact-forms", "Contact forms", "Write a handoff and stop.",
            Asked, Asked.AddHours(4));
}
