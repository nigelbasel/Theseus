using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Settings;

namespace Theseus.Tests.Duties;

/// <summary>
/// The scheduler's rules: a due duty runs, waits or is skipped by its card's state; catch-up folds missed runs into one; the
/// limits hold; ends end it; and a run in flight is followed to its outcome.
/// </summary>
[TestFixture]
public class DutySchedulerTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateTimeOffset SetAt = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DutySettings Settings = new();

    private static Duty DueDuty(Func<DutyRunState, DutyRunState>? state = null) =>
        new("d-00000001", "card", "Check", "do the check", DutyCadence.Every(TimeSpan.FromMinutes(20)), DutyEnd.Never, null, SetAt,
            (state ?? (each => each))(new DutyRunState(SetAt.AddMinutes(20))));

    private static DutyCard Idle() => new("card", "Card", SessionLifecycle.Idle, true);

    private static DutyDecision Decide(Duty duty, DutyCard card, DateTimeOffset now, DutySettings? settings = null) =>
        DutyScheduler.Decide([duty], new Dictionary<string, DutyCard> { ["card"] = card }, settings ?? Settings, now, Utc).Single();

    [Test]
    public void NotYetDue_DoesNothing()
    {
        Assert.That(Decide(DueDuty(), Idle(), SetAt.AddMinutes(19)).Action, Is.EqualTo(DutyAction.None));
    }

    [Test]
    public void Due_OnAnIdleCard_Runs()
    {
        var decision = Decide(DueDuty(), Idle(), SetAt.AddMinutes(21));

        Assert.That((decision.Action, decision.Missed), Is.EqualTo((DutyAction.Run, 0)));
    }

    [Test]
    public void Due_OnAStoppedCard_Runs()
    {
        Assert.That(Decide(DueDuty(), new DutyCard("card", "Card", SessionLifecycle.Ended, false), SetAt.AddMinutes(21)).Action,
            Is.EqualTo(DutyAction.Run));
    }

    [Test]
    public void AfterDowntime_RunsOnce_SayingHowManyItMissed()
    {
        var decision = Decide(DueDuty(), Idle(), SetAt.AddMinutes(20 + 95));

        Assert.That((decision.Action, decision.Missed), Is.EqualTo((DutyAction.Run, 4)));
    }

    [TestCase(SessionLifecycle.Running, "card busy")]
    [TestCase(SessionLifecycle.Launching, "card busy")]
    [TestCase(SessionLifecycle.NeedsAttention, "it is asking you something")]
    public void Due_OnABusyOrAskingCard_Waits(SessionLifecycle lifecycle, string why)
    {
        var decision = Decide(DueDuty(), new DutyCard("card", "Card", lifecycle, true), SetAt.AddMinutes(21));

        Assert.That((decision.Action, decision.Reason), Is.EqualTo((DutyAction.Wait, why)));
    }

    [Test]
    public void Due_WithAQuestionOpen_Waits_AndIsNeverTypedIntoIt()
    {
        Assert.That(Decide(DueDuty(), Idle() with { IsAsking = true }, SetAt.AddMinutes(21)).Action, Is.EqualTo(DutyAction.Wait));
    }

    [Test]
    public void Due_WhileHandingOver_Waits()
    {
        Assert.That(Decide(DueDuty(), Idle() with { IsStopping = true }, SetAt.AddMinutes(21)).Action, Is.EqualTo(DutyAction.Wait));
    }

    [Test]
    public void Due_OnACardThatIsPausedBlockedOrStopped_IsSkipped_WithWhy()
    {
        var now = SetAt.AddMinutes(21);

        Assert.Multiple(() =>
        {
            Assert.That(Decide(DueDuty(), Idle() with { PausedByOwner = true }, now).Reason, Is.EqualTo("paused by you"));
            Assert.That(Decide(DueDuty(), Idle() with { PausedByCard = "Lead" }, now).Reason, Is.EqualTo("paused by Lead"));
            Assert.That(Decide(DueDuty(), Idle() with { IsBlocked = true }, now).Reason, Is.EqualTo("blocked"));
            Assert.That(Decide(DueDuty(), Idle() with { StoppedByOwner = true }, now).Reason, Is.EqualTo("stopped by you"));
            Assert.That(Decide(DueDuty(), Idle() with { PausedByOwner = true }, now).Action, Is.EqualTo(DutyAction.Skip));
        });
    }

    [Test]
    public void APausedDuty_DoesNotRun_UnlessRunNowIsAsked()
    {
        var paused = DueDuty() with { PausedBy = Duty.Owner };

        Assert.Multiple(() =>
        {
            Assert.That(Decide(paused, Idle(), SetAt.AddMinutes(21)).Action, Is.EqualTo(DutyAction.None));
            Assert.That(Decide(paused with { State = paused.State with { RunNowRequested = true } }, Idle(), SetAt.AddMinutes(1)).Action,
                Is.EqualTo(DutyAction.Run));
        });
    }

    [Test]
    public void RunNow_RunsBeforeItIsDue()
    {
        Assert.That(Decide(DueDuty(state => state with { RunNowRequested = true }), Idle(), SetAt.AddMinutes(1)).Action, Is.EqualTo(DutyAction.Run));
    }

    [Test]
    public void ARunInFlight_IsNotGivenAgain()
    {
        Assert.That(Decide(DueDuty(state => state with { DeliveredUtc = SetAt }), Idle(), SetAt.AddMinutes(41)).Action, Is.EqualTo(DutyAction.None));
    }

    [Test]
    public void TheDailyCap_SkipsTheRestOfTheDay()
    {
        var capped = DueDuty(state => state with { CountedDay = new DateOnly(2026, 11, 24), RunsThatDay = 2 }) with { DailyRunCap = 2 };

        Assert.Multiple(() =>
        {
            Assert.That(Decide(capped, Idle(), SetAt.AddMinutes(21)).Action, Is.EqualTo(DutyAction.Skip));
            Assert.That(Decide(capped with { State = capped.State with { NextDueUtc = new DateTimeOffset(2026, 11, 25, 0, 0, 0, TimeSpan.Zero) } },
                Idle(), new DateTimeOffset(2026, 11, 25, 0, 1, 0, TimeSpan.Zero)).Action, Is.EqualTo(DutyAction.Run), "a new day");
        });
    }

    [Test]
    public void QuietHours_HoldADueRun_UnlessItRunsThroughThem()
    {
        var quiet = new DutySettings { QuietHoursOn = true, QuietFrom = new TimeOnly(22, 0), QuietUntil = new TimeOnly(7, 0) };
        var night = new DateTimeOffset(2026, 11, 24, 23, 0, 0, TimeSpan.Zero);
        var duty = DueDuty(state => state with { NextDueUtc = night.AddMinutes(-1) });

        Assert.Multiple(() =>
        {
            Assert.That(Decide(duty, Idle(), night, quiet).Reason, Is.EqualTo("quiet hours until 07:00"));
            Assert.That(Decide(duty with { ThroughQuietHours = true }, Idle(), night, quiet).Action, Is.EqualTo(DutyAction.Run));
            Assert.That(Decide(duty, Idle(), night, new DutySettings()).Action, Is.EqualTo(DutyAction.Run), "off by default");
        });
    }

    [Test]
    public void Ends_EndTheDuty()
    {
        var now = new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(Decide(DueDuty(), Idle() with { IsArchived = true }, now).Action, Is.EqualTo(DutyAction.End));
            Assert.That(Decide(DueDuty() with { End = new DutyEnd(DutyEndKind.OnDate, new DateOnly(2026, 11, 30)) }, Idle(), now).Action,
                Is.EqualTo(DutyAction.End));
            Assert.That(DutyScheduler.Decide([DueDuty() with { End = new DutyEnd(DutyEndKind.OnCriterion, Criterion: "x") }],
                new Dictionary<string, DutyCard> { ["card"] = Idle() }, Settings, now, Utc, _ => true).Single().Action, Is.EqualTo(DutyAction.End));
        });
    }

    [Test]
    public void ACardMissingFromTheBoard_EndsNothing()
    {
        var decision = DutyScheduler.Decide([DueDuty()], new Dictionary<string, DutyCard>(), Settings, SetAt.AddHours(1), Utc).Single();

        Assert.That(decision.Action, Is.EqualTo(DutyAction.None));
    }

    [Test]
    public void Observe_FinishesOnceTheCardWorkedAndIsIdleAgain()
    {
        var given = DueDuty(state => state with { DeliveredUtc = SetAt });
        var working = DutyScheduler.Observe(given, new DutyCard("card", "Card", SessionLifecycle.Running, true), SetAt.AddMinutes(1));
        var seen = given with { State = given.State with { SeenWorking = true } };
        var done = DutyScheduler.Observe(seen, Idle(), SetAt.AddMinutes(3));

        Assert.Multiple(() =>
        {
            Assert.That((working.Outcome, working.SeenWorking), Is.EqualTo(((DutyOutcome?)null, true)));
            Assert.That(done.Outcome, Is.EqualTo(DutyOutcome.Finished));
            Assert.That(DutyScheduler.Observe(seen, new DutyCard("card", "Card", SessionLifecycle.Ended, false), SetAt.AddMinutes(3)).Outcome,
                Is.EqualTo(DutyOutcome.Finished), "its session ended");
        });
    }

    [Test]
    public void Observe_CountsAShortTurnBetweenTwoLooks_ByItsStatusChange()
    {
        var given = DueDuty(state => state with { DeliveredUtc = SetAt });

        var outcome = DutyScheduler.Observe(given, Idle() with { StatusSinceUtc = SetAt.AddSeconds(20) }, SetAt.AddMinutes(1));

        Assert.That(outcome.Outcome, Is.EqualTo(DutyOutcome.Finished));
    }

    [Test]
    public void Observe_CountsADeadSession_AsACrash_AndAnUntakenRun_AsNotDelivered()
    {
        var given = DueDuty(state => state with { DeliveredUtc = SetAt });

        Assert.Multiple(() =>
        {
            Assert.That(DutyScheduler.Observe(given, new DutyCard("card", "Card", SessionLifecycle.Ghost, true), SetAt.AddMinutes(1)).Outcome,
                Is.EqualTo(DutyOutcome.Crashed));
            Assert.That(DutyScheduler.Observe(given, Idle(), SetAt.AddMinutes(5)).Outcome, Is.Null, "still time to take it up");
            Assert.That(DutyScheduler.Observe(given, Idle(), SetAt.AddMinutes(11)).Outcome, Is.EqualTo(DutyOutcome.NotDelivered));
        });
    }
}
