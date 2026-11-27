using Theseus.Core.Kanban;
using Theseus.Core.Settings;

namespace Theseus.Tests.Kanban;

/// <summary>The kanban's widths, collapsed columns and folded rows, remembered between runs (projects-kanban AC 6, 11, 12, 14).</summary>
[TestFixture]
public class KanbanLayoutTests
{
    [Test]
    public void ANewKanban_HasDoneCollapsed_AndTheRestOpenAtTheDefaultWidth()
    {
        var layout = KanbanLayout.From(TheseusSettings.Default);

        Assert.That(layout.Collapsed, Is.EquivalentTo(new[] { KanbanColumn.Done }));
        Assert.That(layout.WidthOf(KanbanColumn.Spec), Is.EqualTo(KanbanLayout.DefaultWidth));
    }

    [TestCase(20, KanbanLayout.MinWidth)]
    [TestCase(9000, KanbanLayout.MaxWidth)]
    [TestCase(300, 300)]
    public void AWidth_IsKeptWithinItsBounds(double asked, double kept)
    {
        Assert.That(KanbanLayout.Default.WithWidth(KanbanColumn.Plan, asked).WidthOf(KanbanColumn.Plan), Is.EqualTo(kept));
    }

    [Test]
    public void ALayout_RoundTripsThroughTheSettings_AndLeavesTheRestAlone()
    {
        var settings = TheseusSettings.Default with { PaneSizes = new Dictionary<string, double> { ["ProjectView:Pane"] = 400 } };
        var layout = KanbanLayout.Default.WithWidth(KanbanColumn.Build, 320).WithCollapsed(KanbanColumn.Done, false)
            .WithCollapsed(KanbanColumn.Review, true).WithFolded("bug-fixing", true);

        var written = layout.Into(settings);
        var read = KanbanLayout.From(written);

        Assert.That(read.WidthOf(KanbanColumn.Build), Is.EqualTo(320));
        Assert.That(read.Collapsed, Is.EquivalentTo(new[] { KanbanColumn.Review }));
        Assert.That(read.FoldedRows, Is.EquivalentTo(new[] { "bug-fixing" }));
        Assert.That(written.PaneSizes["ProjectView:Pane"], Is.EqualTo(400));
    }

    [Test]
    public void OpeningEveryColumn_IsRemembered_NotResetToTheDefault()
    {
        var read = KanbanLayout.From(KanbanLayout.Default.WithCollapsed(KanbanColumn.Done, false).Into(TheseusSettings.Default));

        Assert.That(read.Collapsed, Is.Empty);
    }
}
