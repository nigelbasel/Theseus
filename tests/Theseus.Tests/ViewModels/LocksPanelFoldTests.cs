using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Locks;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The locks panel as a whole: every lock and every section around them folds, Expand all and Collapse all reach all of
/// them, and the folds come back after a restart (Nigel, 2026-11-26).
/// </summary>
[TestFixture]
public class LocksPanelFoldTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;
    private LockBook _book = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"theseus-locks-folds-{Guid.NewGuid():N}");
        _book = new LockBook(
            new LockStateStore(Path.Combine(_directory, "locks.json")),
            new LockHistory(Path.Combine(_directory, "locks-history.jsonl")),
            new AlwaysAlive(),
            new FixedClock(Nine),
            table: ResourceTable.Seeded());
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
    public void EveryLock_StartsFolded_AndTheSectionsToo()
    {
        using var locks = Panel(new MemorySettingsStore());

        Assert.That(locks.Rows, Is.Not.Empty);
        Assert.That(locks.Rows.Select(row => row.Fold.IsExpanded), Is.All.False);
        Assert.That(new[] { locks.NeedsRulingFold, locks.RulingsFold, locks.DeadlocksFold }.Select(fold => fold.IsExpanded), Is.All.False);
        Assert.That(locks.UnownedFold.IsExpanded, Is.True, "unowned containers wait on the owner, so they show");
    }

    [Test]
    public void ExpandAll_ReachesEveryLockItsQueueAndEverySection_AndCollapseAllFoldsThem()
    {
        using var locks = Panel(new MemorySettingsStore());

        locks.ExpandAllCommand.Execute(null);

        Assert.That(locks.Rows.Select(row => row.Fold.IsExpanded), Is.All.True);
        Assert.That(locks.Rows.Select(row => row.QueueFold.IsExpanded), Is.All.True);
        Assert.That(Sections(locks).Select(fold => fold.IsExpanded), Is.All.True);

        locks.CollapseAllCommand.Execute(null);

        Assert.That(locks.Rows.Select(row => row.Fold.IsExpanded), Is.All.False);
        Assert.That(Sections(locks).Select(fold => fold.IsExpanded), Is.All.False);
    }

    [Test]
    public void TheFolds_ComeBackAfterARestart()
    {
        var store = new MemorySettingsStore();

        using (var first = Panel(store))
        {
            first.Rows.Single(row => row.Name == LockNames.Llama).Fold.IsExpanded = true;
            first.RulingsFold.IsExpanded = true;
        }

        using var again = Panel(store);

        Assert.That(again.Rows.Single(row => row.Name == LockNames.Llama).Fold.IsExpanded, Is.True);
        Assert.That(again.Rows.Single(row => row.Name == LockNames.DevStack).Fold.IsExpanded, Is.False);
        Assert.That(again.RulingsFold.IsExpanded, Is.True);
    }

    [Test]
    public void AnIdleBook_ShowsEveryLockFree()
    {
        using var locks = Panel(new MemorySettingsStore());

        locks.Refresh();

        Assert.That(locks.Rows.Select(row => row.StateText), Is.All.EqualTo("free"));
    }

    private LocksViewModel Panel(MemorySettingsStore store) => new(_book, () => Nine) { Settings = store };

    private static IEnumerable<FoldState> Sections(LocksViewModel locks) =>
        [locks.NeedsRulingFold, locks.RulingsFold, locks.DeadlocksFold, locks.UnownedFold];

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class AlwaysAlive : ILockLiveness
    {
        public string? StartOf(int processId) => null;

        public bool IsSessionAlive(string sessionId) => true;
    }
}
