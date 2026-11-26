using Theseus.App.ViewModels;
using Theseus.Core.Locks;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A lock folded to one line: its name, a short state with the holds and waits counts, an amber mark when it waits on the
/// owner, and a queue that folds on its own (Nigel, 2026-11-26).
/// </summary>
[TestFixture]
public class LockRowFoldTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ALock_StartsFolded_ItsQueueOpen()
    {
        var row = Row();

        Assert.That(row.Fold.IsExpanded, Is.False);
        Assert.That(row.QueueFold.IsExpanded, Is.True);
    }

    [TestCase(0, 0, "free")]
    [TestCase(1, 0, "held by 1")]
    [TestCase(2, 0, "held by 2")]
    [TestCase(0, 3, "free · 3 waiting")]
    [TestCase(1, 3, "held by 1 · 3 waiting")]
    public void TheFoldedLine_SaysItsStateAndCounts(int holds, int waits, string state)
    {
        var table = ResourceTable.Seeded().With(new ResourceRule(ResourceTable.CpuBuild, 3, [], "measured"));
        var row = new LockRowViewModel(ResourceTable.CpuBuild, _ => { }, _ => { }, _ => { }, _ => { });
        List<LockTicket> tickets =
        [
            .. Enumerable.Range(0, holds).Select(index => Build($"h{index}", Nine)),
            .. Enumerable.Range(0, waits).Select(index => Build($"w{index}", null)),
        ];

        row.Update(tickets, Nine, table: table);

        Assert.That(row.StateText, Is.EqualTo(state));
        Assert.That((row.HoldCount, row.WaitCount), Is.EqualTo((holds, waits)));
        Assert.That(row.NeedsOwner, Is.False);
    }

    [Test]
    public void ALockWithStoppedDeadlocks_SaysSo()
    {
        var row = Row();

        row.Update([], Nine, stoppedDeadlocks: 2);

        Assert.That(row.StateText, Is.EqualTo("free · 2 deadlocks stopped"));
    }

    [Test]
    public void AnIdleHold_ShowsItsAmberMarkFolded_InWordsAsWellAsColour()
    {
        var row = Row(LockNames.DevStack);

        row.Update([Session()], Nine, idleOf: _ => "session idle 42m");

        Assert.That(row.NeedsOwner, Is.True);
        Assert.That(row.StateText, Is.EqualTo("held by 1 · ⚠ idle"));
        Assert.That(row.ScreenName, Is.EqualTo("dev-stack, held by 1, idle hold, release it?"));
    }

    [Test]
    public void ALongHold_ShowsItsAmberMarkFolded()
    {
        var row = Row();

        row.Update([Holder(Nine - LocksViewModel.LongHold - TimeSpan.FromMinutes(1))], Nine);

        Assert.That(row.NeedsOwner, Is.True);
        Assert.That(row.StateText, Is.EqualTo("held by 1 · ⚠ long"));
    }

    [Test]
    public void ALockThatGainsAnOwnerAction_StaysFolded_WithItsMarkOnItsLine()
    {
        var row = Row(LockNames.DevStack);
        row.Update([Session()], Nine);

        row.Update([Session()], Nine, idleOf: _ => "session idle 42m");

        Assert.That(row.Fold.IsExpanded, Is.False, "unfolding every long hold brought the busy panel back");
        Assert.That(row.NeedsOwner, Is.True);
        Assert.That(row.StateText, Does.EndWith("⚠ idle"));
    }

    [Test]
    public void TheQueue_FoldsIndependentlyOfItsLock()
    {
        var row = Row();
        row.Fold.IsExpanded = true;

        row.QueueFold.IsExpanded = false;

        Assert.That(row.Fold.IsExpanded, Is.True);
        Assert.That(row.QueueHeading, Is.EqualTo("Waiting, in order (0)"));
    }

    [Test]
    public void TheQueueHeading_CountsTheQueue()
    {
        var row = Row();

        row.Update([Holder(Nine), Waiter("a"), Waiter("b")], Nine);

        Assert.That(row.QueueHeading, Is.EqualTo("Waiting, in order (2)"));
    }

    [Test]
    public void Folds_AreSharedThroughTheBook_SoExpandAllReachesTheRow()
    {
        var folds = new LockFolds();
        var row = new LockRowViewModel(LockNames.Tests, _ => { }, _ => { }, _ => { }, _ => { }, folds);

        folds.ExpandAllCommand.Execute(null);

        Assert.That(row.Fold.IsExpanded, Is.True);
        Assert.That(row.QueueFold.IsExpanded, Is.True);
    }

    private static LockRowViewModel Row(string name = LockNames.Tests) => new(name, _ => { }, _ => { }, _ => { }, _ => { });

    private static LockTicket Build(string token, DateTimeOffset? granted) =>
        new(token, "cpu-build", token, "build", HoldMode.Process, token, Nine, Nine, GrantedUtc: granted,
            Scope: [new ResourceClaim(ResourceTable.CpuBuild, 1)]);

    private static LockTicket Holder(DateTimeOffset granted) =>
        new("holder", LockNames.Tests, "H", "x", HoldMode.Process, "h", granted, granted, GrantedUtc: granted);

    private static LockTicket Waiter(string token) =>
        new(token, LockNames.Tests, token, "suite", HoldMode.Process, token, Nine, Nine);

    private static LockTicket Session() =>
        new("s", LockNames.DevStack, "LeadPro", "synthetic data", HoldMode.Session, "k", Nine, Nine, SessionId: "session-1", GrantedUtc: Nine);
}
