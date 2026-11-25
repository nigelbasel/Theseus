using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What the close step does with one session: close it, wait for its quiet period to end, retire a stop that belongs
/// to an earlier run, or leave it (close-step spec AC 1, 4 and 8).
/// </summary>
[TestFixture]
public class CloseDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Asked = Now.AddMinutes(-5);
    private static readonly DateTimeOffset StartedBeforeAsking = Asked.AddHours(-2);

    [Test]
    public void AQuietSessionThatHasHandedOver_IsClosed()
    {
        Assert.That(Decide().Verdict, Is.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void ASessionThatOnlyLacksQuiet_WaitsUntilItsQuietPeriodEnds()
    {
        var wentIdle = Now.AddSeconds(-3);

        var decision = Decide(statusSince: wentIdle);

        Assert.That(decision.Verdict, Is.EqualTo(CloseVerdict.Wait));
        Assert.That(decision.QuietEndsUtc, Is.EqualTo(wentIdle + SessionClosing.Quiet),
            "the refresh its own change caused always comes before its quiet ends, so nothing else would check again");
    }

    [Test]
    public void ACloseOrASkip_CarriesNoWaitingTime()
    {
        Assert.That(Decide().QuietEndsUtc, Is.Null);
        Assert.That(Decide(status: "busy").QuietEndsUtc, Is.Null);
    }

    [TestCase("busy")]
    [TestCase("waiting")]
    public void ASessionThatIsNotIdle_IsLeftAndNotWaitedFor(string status)
    {
        Assert.That(Decide(status: status).Verdict, Is.EqualTo(CloseVerdict.Skip),
            "only quiet is waited for; a working or asking session is checked again when it changes");
    }

    [Test]
    public void ASessionWhoseRunWroteNoHandoff_IsLeftAndNotWaitedFor()
    {
        Assert.That(Decide(written: StartedBeforeAsking.AddMinutes(-30), statusSince: Now.AddSeconds(-3)).Verdict,
            Is.EqualTo(CloseVerdict.Skip));
    }

    [Test]
    public void ASessionNobodyAskedToStop_IsLeft()
    {
        Assert.That(SessionClosing.Decide(null, StartedBeforeAsking, Asked.AddMinutes(1), "idle",
            Now.AddMinutes(-1), Now, alreadyAsked: false).Verdict, Is.EqualTo(CloseVerdict.Skip));
    }

    [Test]
    public void AStopAlreadyTypedFor_IsNeitherClosedAgainNorWaitedFor()
    {
        Assert.That(Decide(alreadyAsked: true).Verdict, Is.EqualTo(CloseVerdict.Skip));
        Assert.That(Decide(alreadyAsked: true, statusSince: Now.AddSeconds(-3)).Verdict, Is.EqualTo(CloseVerdict.Skip));
    }

    [Test]
    public void AStopHandedToAnEarlierRun_IsRetired()
    {
        var handed = Request() with { DeliveredUtc = Asked.AddSeconds(30) };

        Assert.That(Decide(request: handed, sessionStarted: Asked.AddMinutes(2)).Verdict, Is.EqualTo(CloseVerdict.Retire));
    }

    [Test]
    public void AStopAskedBeforeAResumeButHandedToIt_IsClosedNotRetired()
    {
        var resumed = Asked.AddMinutes(1);
        var handedToTheResumedRun = Request() with { DeliveredUtc = resumed.AddSeconds(40) };

        Assert.That(Decide(request: handedToTheResumedRun, sessionStarted: resumed).Verdict, Is.EqualTo(CloseVerdict.Close),
            "resumed-stop review S2: the press and the close step now judge by the same time, when it was handed over");
    }

    private static CloseDecision Decide(
        PendingStopRequest? request = null,
        DateTimeOffset? sessionStarted = null,
        string? status = "idle",
        DateTimeOffset? written = null,
        DateTimeOffset? statusSince = null,
        bool alreadyAsked = false) =>
        SessionClosing.Decide(
            request ?? Request(),
            sessionStarted ?? StartedBeforeAsking,
            written ?? Asked.AddMinutes(1),
            status,
            statusSince ?? Now.AddMinutes(-1),
            Now,
            alreadyAsked);

    private static PendingStopRequest Request() => new(
        "8a3c4e10-5dca-4ac2-9aa3-eeaf67e27e38", "testing-stop", "testing stop",
        "Please stop.", Asked, Asked.AddHours(4))
    {
        HandoffPath = @"C:\notes\HANDOFF-testing-stop.md",
    };
}
