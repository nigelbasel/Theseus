using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class SleepTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private SleepClock _clock = null!;
    private LockBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-sleep-{Guid.NewGuid():N}");
        _clock = new SleepClock(Nine);
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new SleepLiveness(),
            _clock);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void AnHourAsleep_DropsNoWaiter_AndEveryoneKeepsTheirPlace()
    {
        LockAnswer holder = Ask("holder");
        LockAnswer first = Ask("first");
        LockAnswer second = Ask("second");
        _book.Sweep();

        _clock.Advance(TimeSpan.FromMinutes(60));
        _book.Sweep();

        Assert.That(_book.Check(first.Token!).Position, Is.EqualTo(1));
        Assert.That(_book.Check(second.Token!).Position, Is.EqualTo(2));
        Assert.That(_book.Check(holder.Token!).Status, Is.EqualTo(LockStatus.Granted), "an unattached grant is not lapsed by sleep either");
    }

    [Test]
    public void AWaiterSilentWhileTheMachineIsAwake_IsStillDropped()
    {
        _book.Request(new LockRequest(LockNames.Tests, "holder", "session work", HoldMode.Session, SessionId: "S"));
        LockAnswer silent = Ask("silent");

        for (int step = 0; step < 24; step++)
        {
            _clock.Advance(TimeSpan.FromSeconds(15));
            _book.Sweep();
        }

        Assert.That(_book.Check(silent.Token!).Status, Is.EqualTo(LockStatus.Ended));
    }

    private LockAnswer Ask(string who) =>
        _book.Request(new LockRequest(LockNames.Tests, who, $"{who} suite", HoldMode.Process));

    private sealed class SleepClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }

    private sealed class SleepLiveness : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
