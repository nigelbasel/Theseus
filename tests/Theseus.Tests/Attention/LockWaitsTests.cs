using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Locks;

namespace Theseus.Tests.Attention;

/// <summary>7.3: a session's time queued for a lock is taken out of machine time and shown on its own.</summary>
[TestFixture]
public class LockWaitsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void From_TakesEachGrantsWait_WithTheCardItWasTakenFor()
    {
        var events = new[]
        {
            new LockEvent(At(0), LockEventKind.Requested, "tests", "Alpha", "suite") { Card = "alpha", Session = "s1" },
            new LockEvent(At(20), LockEventKind.Granted, "tests", "Alpha", "suite", WaitedSeconds: 1200) { Card = "alpha", Session = "s1" },
            new LockEvent(At(30), LockEventKind.Released, "tests", "Alpha", "suite", WaitedSeconds: 1200, HeldSeconds: 600) { Card = "alpha" },
        };

        Assert.That(LockWaits.From(events, []), Is.EqualTo(new[] { new LockWait("alpha", "s1", "tests", At(0), At(20)) }),
            "a release repeats the wait, so only the grant counts");
    }

    [Test]
    public void From_MatchesALineFromBefore73ToItsCardByTheHoldersName()
    {
        var events = new[] { new LockEvent(At(20), LockEventKind.Granted, "tests", "Theseus 7.3: all time", "suite", WaitedSeconds: 600) };

        Assert.That(LockWaits.From(events, [Card("theseus-7-3", "Theseus 7.3: all time")]).Single().Workstream, Is.EqualTo("theseus-7-3"));
    }

    [Test]
    public void From_CountsAWaiterThatLeftTheQueueWithoutTheLock()
    {
        var events = new[] { new LockEvent(At(5), LockEventKind.WaiterDropped, "tests", "Alpha", "suite", WaitedSeconds: 300) { Card = "alpha" } };

        Assert.That(LockWaits.From(events, []).Single().From, Is.EqualTo(At(0)));
    }

    [Test]
    public void IssueTime_TakesALockWaitOutOfMachineTime_AndShowsItOnItsOwn()
    {
        var marks = new[] { new WorkMark("s1", "alpha", WorkMarkKind.Start, "KITE-1", null, At(0)) };
        var intervals = new[] { new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(60)) { Session = "s1" } };
        var waits = new[] { new LockWait("alpha", "s1", "tests", At(10), At(30)) };

        var time = IssueTimeCalculator.For("KITE-1", marks, intervals, At(0), At(60), At(60), waits);

        Assert.That((time.Machine, time.LockWait), Is.EqualTo((TimeSpan.FromMinutes(40), TimeSpan.FromMinutes(20))));
    }

    [Test]
    public void IssueTime_TakesAWaitOnlyFromItsOwnSessionsTime()
    {
        var marks = new[] { new WorkMark("s1", "alpha", WorkMarkKind.Start, "KITE-1", null, At(0)) };
        var intervals = new[]
        {
            new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(60)) { Session = "s1" },
            new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(60)) { Session = "s2" },
        };
        var waits = new[] { new LockWait("alpha", "s1", "tests", At(10), At(30)) };

        var time = IssueTimeCalculator.For("KITE-1", marks, intervals, At(0), At(60), At(60), waits);

        Assert.That(time.Machine, Is.EqualTo(TimeSpan.FromMinutes(100)), "the other session was working, not waiting");
    }

    private static Workstream Card(string key, string title) =>
        new(key, title, "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.", 1, "New",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, null, [], null, DateTimeOffset.UtcNow);

    private static DateTimeOffset At(int minutes) => Nine.AddMinutes(minutes);
}
