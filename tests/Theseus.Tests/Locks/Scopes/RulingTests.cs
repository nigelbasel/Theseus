using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.Locks.Scopes;

[TestFixture]
public class RulingTests
{
    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"theseus-rulings-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void TheTable_IsSeededOnFirstUse()
    {
        Open();

        Assert.That(File.ReadAllText(Path.Combine(_directory, "resources.json")), Does.Contain("sql-containers").And.Contain("compose:*"));
    }

    [Test]
    public void AResourceNobodyRuledOn_IsListedForARuling_AndWaitsForEveryHold()
    {
        LockBook book = Open();
        Ask(book, "llama");

        LockAnswer gpu = Ask(book, "gpu");

        Assert.That(gpu.Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(book.NeedsRuling(), Is.EqualTo(new[] { "gpu" }));
    }

    [Test]
    public void ARuling_LetsWaitersThatNoLongerClashGo_IsSaved_AndSurvivesARestart()
    {
        LockBook book = Open();
        Ask(book, "llama");
        LockAnswer gpu = Ask(book, "gpu");

        book.Rule(new ResourceRule("gpu", 1, ["dev-stack"], "the card uses its own GPU slot"), "arbiter session");

        Assert.That(book.Check(gpu.Token!).Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(book.NeedsRuling(), Is.Empty);

        ResourceRule saved = Open().Table.Rule("gpu")!;
        Assert.That(saved.RuledBy, Is.EqualTo("arbiter session"));
        Assert.That(saved.ClashesWith, Is.EqualTo(new[] { "dev-stack" }));
    }

    [Test]
    public void OverturningARuling_MakesTheResourceExclusiveOfEverythingAgain()
    {
        LockBook book = Open();
        book.Rule(new ResourceRule("gpu", 1, [], "own slot"), "arbiter session");

        book.Overturn("gpu");
        Ask(book, "llama");

        Assert.That(Ask(book, "gpu").Status, Is.EqualTo(LockStatus.Waiting));
        Assert.That(Open().Table.IsKnown("gpu"), Is.False);
    }

    [Test]
    public void TheTestsScope_TakesEveryBuildSlot_EvenAfterTheSlotsAreRuledUp()
    {
        LockBook book = Open();
        LockAnswer tests = book.Request(new LockRequest(LockNames.Tests, "old", "suite", HoldMode.Process));

        book.Rule(new ResourceRule(ResourceTable.CpuBuild, 3, [], "measured"), "Nigel");

        Assert.That(tests.Status, Is.EqualTo(LockStatus.Granted));
        Assert.That(Ask(book, "cpu-build").Status, Is.EqualTo(LockStatus.Waiting));
    }

    private static LockAnswer Ask(LockBook book, string resource) =>
        book.Request(new LockRequest(string.Empty, $"{resource} user", $"{resource} work", HoldMode.Process, Scope: [new ResourceClaim(resource, 1)]));

    private LockBook Open() =>
        new(new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new RulingLiveness(),
            new RulingClock(),
            tableStore: new ResourceTableStore(Path.Combine(_directory, "resources.json")));

    private sealed class RulingClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    }

    private sealed class RulingLiveness : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
