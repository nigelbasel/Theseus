using Theseus.Core.Duties;

namespace Theseus.Tests.Duties;

/// <summary>
/// The duty book: setting and changing duties, who may change them, run bookkeeping, the self-pause after failures, the
/// queue's memory, and everything surviving a restart.
/// </summary>
[TestFixture]
public class DutyBookTests
{
    private static readonly DateTimeOffset SetAt = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DutyCadence Twenty = DutyCadence.Every(TimeSpan.FromMinutes(20));
    private string _path = string.Empty;
    private DutyTestClock _clock = null!;

    [SetUp]
    public void SetUp()
    {
        _path = Path.Combine(Path.GetTempPath(), $"theseus-duties-{Guid.NewGuid():N}.json");
        _clock = new DutyTestClock(SetAt);
    }

    [TearDown]
    public void TearDown() => File.Delete(_path);

    private DutyBook Book() => new(new DutyStore(_path), _clock, TimeZoneInfo.Utc);

    [Test]
    public void Set_MakesADuty_FirstDueOneIntervalOn()
    {
        var (duty, updated, refusal) = Book().Set("card", "Check", "do the check", Twenty, DutyEnd.Never, null);

        Assert.Multiple(() =>
        {
            Assert.That(refusal, Is.Null);
            Assert.That(updated, Is.False);
            Assert.That(duty!.Id, Does.Match("^d-[0-9a-f]{8}$"));
            Assert.That(duty.State.NextDueUtc, Is.EqualTo(SetAt.AddMinutes(20)));
            Assert.That(duty.IsOwners, Is.True);
        });
    }

    [Test]
    public void Set_AgainByName_ChangesIt_AndANewCadenceCountsFromNow()
    {
        var book = Book();
        var (first, _, _) = book.Set("card", "Check", "do the check", Twenty, DutyEnd.Never, "lead");
        _clock.UtcNow = SetAt.AddMinutes(7);
        var (same, sameUpdated, _) = book.Set("card", "check", "do it better", Twenty, DutyEnd.Never, "lead");
        var (hourly, _, _) = book.Set("card", "Check", "do it better", DutyCadence.Every(TimeSpan.FromHours(1)), DutyEnd.Never, "lead");

        Assert.Multiple(() =>
        {
            Assert.That((same!.Id, sameUpdated, same.Instruction), Is.EqualTo((first!.Id, true, "do it better")));
            Assert.That(same.State.NextDueUtc, Is.EqualTo(SetAt.AddMinutes(20)), "the same cadence keeps its grid");
            Assert.That(hourly!.State.NextDueUtc, Is.EqualTo(SetAt.AddMinutes(67)));
            Assert.That(book.For("card"), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void Set_ByASession_IsRefused_OnADutyTheOwnerSet()
    {
        var book = Book();
        book.Set("card", "Check", "do the check", Twenty, DutyEnd.Never, null);

        var (duty, _, refusal) = book.Set("card", "Check", "something else", Twenty, DutyEnd.Never, "lead");

        Assert.Multiple(() =>
        {
            Assert.That(duty, Is.Null);
            Assert.That(refusal, Does.Contain("set by the user"));
        });
    }

    [Test]
    public void Set_RefusesNoName_NoInstruction_ATooShortInterval_AndACapUnderOne()
    {
        var book = Book();

        Assert.Multiple(() =>
        {
            Assert.That(book.Set("card", " ", "x", Twenty, DutyEnd.Never, null).Refusal, Is.Not.Null);
            Assert.That(book.Set("card", "n", " ", Twenty, DutyEnd.Never, null).Refusal, Is.Not.Null);
            Assert.That(book.Set("card", "n", "x", DutyCadence.Every(TimeSpan.FromMinutes(1)), DutyEnd.Never, null).Refusal, Is.Not.Null);
            Assert.That(book.Set("card", "n", "x", Twenty, DutyEnd.Never, null, dailyRunCap: 0).Refusal, Is.Not.Null);
        });
    }

    [Test]
    public void Ran_MovesTheNextRunOn_AndCountsTheDay()
    {
        var book = Book();
        var id = book.Set("card", "Check", "x", Twenty, DutyEnd.Never, null).Duty!.Id;
        _clock.UtcNow = SetAt.AddMinutes(65);

        var ran = book.Ran(id)!;

        Assert.Multiple(() =>
        {
            Assert.That(ran.State.NextDueUtc, Is.EqualTo(SetAt.AddMinutes(80)), "on the grid, not 20 minutes from the late run");
            Assert.That((ran.State.RunCount, ran.State.RunsOn(new DateOnly(2026, 11, 24))), Is.EqualTo((1, 1)));
            Assert.That(ran.State.DeliveredUtc, Is.EqualTo(_clock.UtcNow));
        });
    }

    [Test]
    public void RunNow_DoesNotMoveTheCadence()
    {
        var book = Book();
        var id = book.Set("card", "Check", "x", Twenty, DutyEnd.Never, null).Duty!.Id;
        _clock.UtcNow = SetAt.AddMinutes(5);
        book.RunNow(id);

        var ran = book.Ran(id)!;

        Assert.Multiple(() =>
        {
            Assert.That(ran.State.NextDueUtc, Is.EqualTo(SetAt.AddMinutes(20)));
            Assert.That(ran.State.RunNowRequested, Is.False);
        });
    }

    [Test]
    public void Outcome_PausesTheDutyAfterTheFailureLimit_AndAFinishedRunClearsTheCount()
    {
        var book = Book();
        var id = book.Set("card", "Check", "x", Twenty, DutyEnd.Never, null).Duty!.Id;

        book.Outcome(id, DutyOutcome.Crashed, "died", 3);
        book.Outcome(id, DutyOutcome.Finished, null, 3);
        book.Outcome(id, DutyOutcome.Crashed, "died", 3);
        book.Outcome(id, DutyOutcome.NotDelivered, "not woken", 3);
        var (duty, paused) = book.Outcome(id, DutyOutcome.Crashed, "died again", 3);

        Assert.Multiple(() =>
        {
            Assert.That(paused, Is.True);
            Assert.That(duty!.PausedBy, Is.EqualTo(Duty.Theseus));
            Assert.That(duty.PauseReason, Is.EqualTo("paused by Theseus after 3 failed runs: died again"));
            Assert.That(book.Resume(id)!.State.FailuresInRow, Is.EqualTo(0));
        });
    }

    [Test]
    public void EverythingSurvivesARestart_AndEndedDutiesGoAfterThirtyDays()
    {
        var book = Book();
        var kept = book.Set("card", "Kept", "x", Twenty, DutyEnd.Never, null).Duty!.Id;
        var ended = book.Set("card", "Ended", "x", Twenty, DutyEnd.Never, null).Duty!.Id;
        book.Ran(kept);
        book.End(ended, "done");
        book.QueueResumed("card", "Build: step 2");

        var reopened = Book();
        _clock.UtcNow = SetAt.AddDays(31);
        var later = Book();

        Assert.Multiple(() =>
        {
            Assert.That(reopened.Get(kept)!.State.RunCount, Is.EqualTo(1));
            Assert.That(reopened.Get(ended)!.EndReason, Is.EqualTo("done"));
            Assert.That(reopened.QueueRecordFor("card")!.StatusAtResume, Is.EqualTo("Build: step 2"));
            Assert.That(later.Get(ended), Is.Null);
            Assert.That(later.Get(kept), Is.Not.Null);
        });
    }

    [Test]
    public void AFileThatCannotBeRead_IsTreatedAsEmpty()
    {
        File.WriteAllText(_path, "{ not json");

        Assert.That(Book().All, Is.Empty);
    }

    [Test]
    public void ASessionsPause_NeverReplacesTheOwnersOrTheseuss()
    {
        var book = Book();
        var id = book.Set("card", "Check", "x", Twenty, DutyEnd.Never, "lead").Duty!.Id;
        book.Pause(id, Duty.Owner, "not tonight");

        var after = book.Pause(id, "lead", "mine now")!;

        Assert.That((after.PausedBy, after.PauseReason), Is.EqualTo((Duty.Owner, "not tonight")));
    }

    [Test]
    public void AnOwnersEdit_MakesTheDutyHis()
    {
        var book = Book();
        book.Set("card", "Check", "x", Twenty, DutyEnd.Never, "lead");

        var (edited, _, _) = book.Set("card", "Check", "y", Twenty, DutyEnd.Never, null);

        Assert.That(edited!.IsOwners, Is.True);
    }

    [Test]
    public void Delete_RemovesIt()
    {
        var book = Book();
        var id = book.Set("card", "Check", "x", Twenty, DutyEnd.Never, null).Duty!.Id;

        Assert.Multiple(() =>
        {
            Assert.That(book.Delete(id), Is.True);
            Assert.That(book.Get(id), Is.Null);
        });
    }
}
