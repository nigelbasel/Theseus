using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>The locks panel's folds: their defaults, Expand all and Collapse all, and that they are remembered (Nigel, 2026-11-26).</summary>
[TestFixture]
public class LockFoldsTests
{
    [Test]
    public void AFoldNeverTouched_TakesItsDefault()
    {
        var folds = new LockFolds();

        Assert.That(folds.For("lock:tests", open: false).IsExpanded, Is.False);
        Assert.That(folds.For("queue:tests", open: true).IsExpanded, Is.True);
    }

    [Test]
    public void TheSameKey_GivesTheSameFold()
    {
        var folds = new LockFolds();

        var first = folds.For("lock:tests", open: false);

        Assert.That(folds.For("lock:tests", open: true), Is.SameAs(first));
    }

    [Test]
    public void AFoldChangedByHand_IsRemembered_AndComesBackAfterARestart()
    {
        var store = new MemorySettingsStore();
        var folds = new LockFolds { Settings = store };

        folds.For("lock:tests", open: false).IsExpanded = true;
        folds.For("queue:tests", open: true).IsExpanded = false;

        var again = new LockFolds { Settings = store };

        Assert.That(store.Settings.LocksFolds["lock:tests"], Is.True);
        Assert.That(again.For("lock:tests", open: false).IsExpanded, Is.True);
        Assert.That(again.For("queue:tests", open: true).IsExpanded, Is.False);
    }

    [Test]
    public void ExpandAll_OpensEveryFold_AndCollapseAll_FoldsEveryOne()
    {
        var folds = new LockFolds();
        var lockFold = folds.For("lock:tests", open: false);
        var queue = folds.For("queue:tests", open: true);
        var section = folds.For("section:rulings", open: false);

        folds.ExpandAllCommand.Execute(null);
        Assert.That(new[] { lockFold.IsExpanded, queue.IsExpanded, section.IsExpanded }, Is.All.True);

        folds.CollapseAllCommand.Execute(null);
        Assert.That(new[] { lockFold.IsExpanded, queue.IsExpanded, section.IsExpanded }, Is.All.False);
    }

    [Test]
    public void ExpandAll_IsRemembered_WithOneWrite()
    {
        var store = new MemorySettingsStore();
        var folds = new LockFolds { Settings = store };
        folds.For("lock:tests", open: false);
        folds.For("lock:llama", open: false);
        var before = store.Writes;

        folds.ExpandAllCommand.Execute(null);

        Assert.That(store.Writes - before, Is.EqualTo(1));
        Assert.That(store.Settings.LocksFolds.Values, Is.All.True);
        Assert.That(store.Settings.LocksFolds.Keys, Is.EquivalentTo(new[] { "lock:tests", "lock:llama" }));
    }

    [Test]
    public void TheRememberedFolds_AreKeptByTheSettingsFile()
    {
        var settings = TheseusSettings.Default with { LocksFolds = new Dictionary<string, bool> { ["lock:tests"] = true } };

        Assert.That(settings.LocksFolds["lock:tests"], Is.True);
        Assert.That(TheseusSettings.Default.LocksFolds, Is.Empty);
    }
}
