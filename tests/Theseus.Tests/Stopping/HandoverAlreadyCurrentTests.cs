using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// A session told to stop whose handover is already current says so and leaves the file alone. Seen live on 2026-11-24 with
/// three sessions, each asked to stop, each answering, none ever closed: their handovers were written by the same run,
/// before the stop.
/// </summary>
[TestFixture]
public class HandoverAlreadyCurrentTests
{
    private static readonly DateTimeOffset RunStarted = new(2026, 11, 24, 13, 40, 47, TimeSpan.Zero);
    private static readonly DateTimeOffset HandoverWritten = new(2026, 11, 24, 13, 58, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Asked = new(2026, 11, 24, 14, 51, 9, TimeSpan.Zero);
    private static readonly DateTimeOffset Delivered = Asked.AddSeconds(1);
    private static readonly DateTimeOffset TurnEnded = Asked.AddSeconds(8);
    private static readonly DateTimeOffset Now = TurnEnded + SessionClosing.Quiet + TimeSpan.FromSeconds(1);

    [Test]
    public void AHandoverWrittenByThisRun_Closes_OnceTheTurnThatTookTheStopHasEnded()
    {
        Assert.That(SessionClosing.Decide(Request(), RunStarted, HandoverWritten, "idle", TurnEnded, Now, alreadyAsked: false).Verdict,
            Is.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void AHandoverWrittenByThisRun_WaitsWhileTheTurnThatTookTheStopIsStillGoing()
    {
        Assert.That(SessionClosing.Decide(Request(), RunStarted, HandoverWritten, "busy", Delivered, Now, alreadyAsked: false).Verdict,
            Is.Not.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void AnIdleSpellThatBeganBeforeTheStopWasHandedOver_IsNotTheTurnThatTookIt()
    {
        Assert.That(SessionClosing.Decide(Request(), RunStarted, HandoverWritten, "idle", Asked.AddMinutes(-10), Now, alreadyAsked: false).Verdict,
            Is.Not.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void ATurnEndingLongAfterTheStop_IsSomeoneWorkingWithIt_AndDoesNotClose()
    {
        var later = Delivered + SessionClosing.AnswerWindow + TimeSpan.FromMinutes(1);

        Assert.That(SessionClosing.Decide(Request(), RunStarted, HandoverWritten, "idle", later, later + TimeSpan.FromMinutes(1),
            alreadyAsked: false).Verdict, Is.Not.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void AHandoverFromAnEarlierRun_StillDoesNotClose()
    {
        Assert.That(SessionClosing.Decide(Request(), RunStarted, RunStarted.AddMinutes(-5), "idle", TurnEnded, Now, alreadyAsked: false).Verdict,
            Is.Not.EqualTo(CloseVerdict.Close), "a run that never wrote its handover has not handed over");
    }

    private static PendingStopRequest Request() =>
        new("429eb8e6", "wb7", "WB7", "Please stop.", Asked, Asked.AddHours(4)) { HandoffPath = @"C:\notes\HANDOFF-wb7.md", DeliveredUtc = Delivered };
}
