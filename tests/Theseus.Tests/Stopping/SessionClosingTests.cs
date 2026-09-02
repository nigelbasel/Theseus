using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class SessionClosingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Asked = Now.AddMinutes(-5);

    [Test]
    public void ShouldClose_ClosesASessionThatWasAskedToStopAndHasHandedOver()
    {
        Assert.That(Close(), Is.True);
    }

    [Test]
    public void ShouldClose_LeavesASessionNobodyAskedToStop()
    {
        Assert.That(SessionClosing.ShouldClose(null, Asked.AddMinutes(1), "idle",
            Now.AddMinutes(-1), Now, alreadyAsked: false), Is.False,
            "this only finishes what stopping started; it is not a reaper");
    }

    [Test]
    public void ShouldClose_LeavesASessionStillWorking()
    {
        Assert.That(Close(status: "busy"), Is.False);
    }

    [Test]
    public void ShouldClose_LeavesASessionThatIsWaitingOnItsUser()
    {
        Assert.That(Close(status: "waiting"), Is.False,
            "it has asked a question, and closing it would throw the question away unanswered");
    }

    [Test]
    public void ShouldClose_LeavesASessionThatHasNotWrittenItsHandover()
    {
        // Called directly rather than through the helper: a null there means "use the default",
        // which is exactly the value this test needs to be absent.
        Assert.That(SessionClosing.ShouldClose(Request(), handoffWrittenUtc: null, "idle",
            Now.AddMinutes(-1), Now, alreadyAsked: false), Is.False,
            "closing it would lose exactly what stopping is for");
    }

    [Test]
    public void ShouldClose_IsNotFooledByAHandoverFromBeforeItWasAsked()
    {
        Assert.That(Close(written: Asked.AddMinutes(-30)), Is.False,
            "yesterday's notes are not this stop's handover");
    }

    [Test]
    public void ShouldClose_WaitsForTheSessionToBeQuietForAWhile()
    {
        Assert.That(Close(statusSince: Now.AddSeconds(-2)), Is.False,
            "a session pausing to think is not a session that has finished");

        Assert.That(Close(statusSince: Now - SessionClosing.Quiet), Is.True);
    }

    [Test]
    public void ShouldClose_ClosesOnlyOnce()
    {
        Assert.That(Close(alreadyAsked: true), Is.False,
            "a second /exit would land in whatever the session moved on to");
    }

    [Test]
    public void ShouldClose_ClosesWhenTheSessionNeverSaidWhenItWentQuiet()
    {
        Assert.That(Close(statusSince: null), Is.True);
    }

    private static bool Close(
        string? status = "idle",
        DateTimeOffset? written = null,
        DateTimeOffset? statusSince = null,
        bool alreadyAsked = false) =>
        SessionClosing.ShouldClose(
            Request(),
            written ?? Asked.AddMinutes(1),
            status,
            statusSince ?? Now.AddMinutes(-1),
            Now,
            alreadyAsked);

    private static PendingStopRequest Request() => new(
        "2b96cd6e-5dca-4ac2-9aa3-eeaf67e27e38", "testing-stop", "testing stop",
        "Please stop.", Asked, Asked.AddHours(4))
    {
        HandoffPath = @"C:\dev\HANDOFF-testing-stop.md",
    };
}
