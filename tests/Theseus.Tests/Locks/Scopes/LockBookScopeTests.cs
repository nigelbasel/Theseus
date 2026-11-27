using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class LockBookScopeTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private ScopeClock _clock = null!;
    private ScopeLiveness _liveness = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-scopes-{Guid.NewGuid():N}");
        _clock = new ScopeClock(Nine);
        _liveness = new ScopeLiveness();
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
    public void ScopesThatDoNotClash_AreGrantedSideBySide()
    {
        LockBook book = Open();

        LockAnswer first = Ask(book, "members-hub", "compose:members-hub", "port:8080");
        LockAnswer second = Ask(book, "leadpro", "compose:leadpro", "port:8081");

        Assert.That(first.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(second.Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void TwoBuildsInDifferentTrees_ShareTheBuildSlots_AndTheOldTestsLockWaitsForBoth()
    {
        LockBook book = Open(Slots(2));

        LockAnswer a = Ask(book, "a", "cpu-build", "worktree:e:/work/a");
        LockAnswer b = Ask(book, "b", "cpu-build", "worktree:e:/work/b");
        LockAnswer tests = book.Request(new LockRequest(LockNames.Tests, "suite", "PersistenceTests", HoldMode.Process));

        Assert.That(a.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(b.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(tests.Status, Is.EqualTo(LockStatus.Waiting));

        book.Release(a.Token!);
        Assert.That(book.Check(tests.Token!).Status, Is.EqualTo(LockStatus.Waiting));

        book.Release(b.Token!);
        Assert.That(book.Check(tests.Token!).Status, Is.EqualTo(LockStatus.Granted));
    }

    [Test]
    public void TwoBuildsInOneTree_DoNotRunTogether()
    {
        LockBook book = Open(Slots(2));

        Ask(book, "a", "cpu-build", "worktree:E:\\work\\a\\");
        LockAnswer again = Ask(book, "b", "cpu-build", "worktree:e:/work/a");

        Assert.That(again.Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void ALaterRequestNeedingNothingAnEarlierWaiterWants_GoesAhead_ButOneNeedingTheSameWaitsItsTurn()
    {
        LockBook book = Open();

        LockAnswer holder = Ask(book, "holder", "sql-containers");
        LockAnswer first = Ask(book, "first", "sql-containers");
        LockAnswer compose = Ask(book, "compose", "compose:x");
        LockAnswer third = Ask(book, "third", "sql-containers");

        Assert.That(compose.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(third.Status, Is.EqualTo(LockStatus.Waiting));

        book.Release(holder.Token!);

        Assert.That(book.Check(first.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.Check(third.Token!).Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void AWaiterOvertakenTwice_HasWhatItNeedsReserved_SoNoLaterRequestTakesIt()
    {
        LockBook book = Open(Slots(2));

        LockAnswer h = Ask(book, "h", "cpu-build");
        LockAnswer wide = book.Request(Scoped("wide", [new ResourceClaim("cpu-build", 2)]));
        LockAnswer b = Ask(book, "b", "cpu-build");
        LockAnswer c = Ask(book, "c", "cpu-build");

        Assert.That(wide.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(b.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(c.Status, Is.EqualTo(LockStatus.Waiting));

        book.Release(h.Token!);
        Assert.That(book.Check(c.Token!).Status, Is.EqualTo(LockStatus.Granted), "the second overtake");

        book.Release(b.Token!);
        LockAnswer d = Ask(book, "d", "cpu-build");
        Assert.That(d.Status, Is.EqualTo(LockStatus.Waiting), "a free slot is held back for the twice-overtaken waiter");

        book.Release(c.Token!);
        Assert.That(book.Check(wide.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.Check(d.Token!).Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void AScopeNamingAResourceNobodyRuledOn_WaitsForEveryHold()
    {
        LockBook book = Open();

        LockAnswer llama = book.Request(new LockRequest(LockNames.Llama, "model", "bench", HoldMode.Process));
        LockAnswer gpu = Ask(book, "gpu", "gpu");

        Assert.That(llama.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(gpu.Status, Is.EqualTo(LockStatus.Waiting));
    }

    [Test]
    public void ATicketSavedBeforeScopes_LoadsAsItsPredefinedScope()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "locks.json"), """
            [ { "token": "old", "lock": "tests", "who": "old run", "purpose": "suite", "mode": "Process",
                "requesterKey": "k", "requestedUtc": "2026-11-25T08:00:00+00:00", "lastSeenUtc": "2026-11-25T08:59:00+00:00",
                "grantedUtc": "2026-11-25T08:00:00+00:00" } ]
            """);
        LockBook book = Open();

        LockAnswer build = Ask(book, "build", "cpu-build");
        LockAnswer stack = book.Request(new LockRequest(LockNames.DevStack, "stack", "up", HoldMode.Process));

        Assert.That(build.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(stack.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.Snapshot().Single(ticket => ticket.Token == "old").Scope, Has.Some.Matches<ResourceClaim>(claim => claim.Resource == "sql-containers"));
    }

    [Test]
    public void ARequestWithNeitherALockNorAScope_IsRefused()
    {
        LockBook book = Open();

        LockAnswer answer = book.Request(new LockRequest(string.Empty, "who", "what", HoldMode.Process));

        Assert.That(answer.Status, Is.EqualTo(LockStatus.Refused));
    }

    private static ResourceTable Slots(int slots) =>
        ResourceTable.Seeded().With(new ResourceRule(ResourceTable.CpuBuild, slots, [], "test"));

    private static LockAnswer Ask(LockBook book, string who, params string[] resources) =>
        book.Request(Scoped(who, [.. resources.Select(resource => new ResourceClaim(resource, 1))]));

    private static LockRequest Scoped(string who, IReadOnlyList<ResourceClaim> scope) =>
        new(string.Empty, who, $"{who} work", HoldMode.Process, Scope: scope);

    private LockBook Open(ResourceTable? table = null) =>
        new(new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            _liveness,
            _clock,
            table: table ?? ResourceTable.Seeded());

    private sealed class ScopeClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan span) => UtcNow += span;
    }

    private sealed class ScopeLiveness : ILockLiveness
    {
        public Dictionary<int, string> Processes { get; } = [];

        public HashSet<string> Sessions { get; } = [];

        public string? StartOf(int processId) => Processes.GetValueOrDefault(processId);

        public bool IsSessionAlive(string sessionId) => Sessions.Contains(sessionId);
    }
}
