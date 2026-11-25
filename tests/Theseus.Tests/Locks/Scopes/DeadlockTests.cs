using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class DeadlockTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private DeadlockClock _clock = null!;
    private DeadlockLiveness _liveness = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-deadlocks-{Guid.NewGuid():N}");
        _clock = new DeadlockClock(Nine);
        _liveness = new DeadlockLiveness();
        _liveness.Sessions.UnionWith(["A", "B", "C"]);
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
    public void TwoSessionCycle_TheRequestClosingItIsRefused_AndNothingElseMoves()
    {
        LockBook book = Open();
        LockAnswer aStack = HoldForSession(book, "A", LockNames.DevStack);
        LockAnswer bSql = Run(book, "B", "sql-containers");
        LockAnswer aWaits = Run(book, "A", "sql-containers");

        LockAnswer bAsks = Run(book, "B", LockNames.DevStack);

        Assert.That(aWaits.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(bAsks.Status, Is.EqualTo(LockStatus.Refused));
        Assert.That(bAsks.Detail, Does.StartWith("Granting this would deadlock:"));
        Assert.That(bAsks.Detail, Does.Contain("session A").And.Contain("session B").And.Contain("dev-stack").And.Contain("sql-containers"));
        Assert.That(bAsks.Detail, Does.EndWith("Release what this session holds and ask again for the whole scope at once."));
        Assert.That(book.Check(aWaits.Token!).Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(book.Check(aStack.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.Check(bSql.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.StoppedDeadlocks(), Has.Count.EqualTo(1));
        Assert.That(book.StoppedDeadlocks()[0].Owners, Is.EquivalentTo(new[] { "session A", "session B" }));
    }

    [Test]
    public void ThreeSessionCycle_TheThirdRequestIsRefused()
    {
        LockBook book = Open();
        HoldForSession(book, "A", LockNames.DevStack);
        HoldForSession(book, "B", LockNames.Llama);
        Run(book, "C", "sql-containers");

        LockAnswer aWaits = Run(book, "A", LockNames.Llama);
        LockAnswer bWaits = Run(book, "B", "sql-containers");
        LockAnswer cAsks = Run(book, "C", LockNames.DevStack);

        Assert.That(aWaits.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(bWaits.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(cAsks.Status, Is.EqualTo(LockStatus.Refused));
        Assert.That(book.StoppedDeadlocks()[0].Owners, Is.EquivalentTo(new[] { "session A", "session B", "session C" }));
    }

    [Test]
    public void CycleThroughSlots_IsRefused()
    {
        LockBook book = Open(Slots(2));
        HoldForSession(book, "A", LockNames.DevStack);
        Run(book, "A", "cpu-build");
        Run(book, "B", "cpu-build", "worktree:e:/work/b");

        LockAnswer aWaits = Run(book, "A", "cpu-build", "worktree:e:/work/a2");
        LockAnswer bAsks = Run(book, "B", LockNames.DevStack);

        Assert.That(aWaits.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(bAsks.Status, Is.EqualTo(LockStatus.Refused));
    }

    [Test]
    public void SlotsHeldBySomeoneWaitingForNothing_AreNotACycle()
    {
        LockBook book = Open(Slots(3));
        HoldForSession(book, "A", LockNames.DevStack);
        Run(book, "A", "cpu-build");
        Run(book, "B", "cpu-build", "worktree:e:/work/b");
        LockAnswer c = Run(book, "C", "cpu-build", "worktree:e:/work/c");

        LockAnswer aWaits = Run(book, "A", "cpu-build", "worktree:e:/work/a2");
        LockAnswer bAsks = Run(book, "B", LockNames.DevStack);

        Assert.That(aWaits.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(bAsks.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(book.StoppedDeadlocks(), Is.Empty);

        book.Release(c.Token!);
        Assert.That(book.Check(aWaits.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void ASessionAskingForWhatItAlreadyHolds_IsRefused()
    {
        LockBook book = Open();
        HoldForSession(book, "A", LockNames.DevStack);

        LockAnswer again = Run(book, "A", LockNames.DevStack);

        Assert.That(again.Status, Is.EqualTo(LockStatus.Refused));
    }

    [Test]
    public void RequestsWithNoKnownOwner_NeverFormACycle()
    {
        LockBook book = Open();
        HoldForSession(book, "A", LockNames.DevStack);
        book.Request(new LockRequest(string.Empty, "script", "suite", HoldMode.Process, Scope: [new ResourceClaim("sql-containers", 1)]));

        LockAnswer anonymous = book.Request(new LockRequest(string.Empty, "other", "stack", HoldMode.Process, Scope: [new ResourceClaim(LockNames.DevStack, 1)]));

        Assert.That(anonymous.Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void AReservationThatWouldCloseACycle_IsNotMade()
    {
        LockBook book = Open(Slots(2));
        LockAnswer c = Run(book, "C", "cpu-build", "worktree:e:/work/c");
        LockAnswer wide = book.Request(new LockRequest(string.Empty, "session A", "wide build", HoldMode.Process,
            Scope: [new ResourceClaim("cpu-build", 2)], Owner: "A"));
        LockAnswer d = Run(book, "C", "cpu-build", "worktree:e:/work/d");
        book.Release(d.Token!);
        Run(book, "B", "cpu-build", "worktree:e:/work/e");
        book.Release(c.Token!);
        Assert.That(book.Snapshot().Single(ticket => ticket.Token == wide.Token).Overtaken, Is.EqualTo(2));

        LockAnswer bAgain = Run(book, "B", "cpu-build", "worktree:e:/work/w");

        Assert.That(bAgain.Status, Is.EqualTo(LockStatus.Granted), "holding the slot back for the wide build would leave B and the wide build waiting on each other");
    }

    private static ResourceTable Slots(int slots) =>
        ResourceTable.Seeded().With(new ResourceRule(ResourceTable.CpuBuild, slots, [], "test"));

    private LockAnswer HoldForSession(LockBook book, string session, string lockName) =>
        book.Request(new LockRequest(lockName, $"session {session}", $"{lockName} work", HoldMode.Session, SessionId: session));

    private static LockAnswer Run(LockBook book, string owner, params string[] resources) =>
        book.Request(new LockRequest(string.Empty, $"session {owner}", $"{string.Join(",", resources)} run", HoldMode.Process,
            Scope: [.. resources.Select(resource => new ResourceClaim(resource, 1))], Owner: owner));

    private LockBook Open(ResourceTable? table = null) =>
        new(new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            _liveness,
            _clock,
            table: table ?? ResourceTable.Seeded());

    private sealed class DeadlockClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;
    }

    private sealed class DeadlockLiveness : ILockLiveness
    {
        public HashSet<string> Sessions { get; } = [];

        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => Sessions.Contains(sessionId);
    }
}
