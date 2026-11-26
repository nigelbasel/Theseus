using System.Diagnostics;
using System.Windows;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// The kanban measured on a real projects folder (projects-kanban spec AC 26): rows, cards, and the time from reading
/// to laid out. It runs only when <c>THESEUS_KANBAN_MEASURE</c> names a folder, so no machine path lives in the repo;
/// the projects are read, never written.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
[Explicit("Measures a real projects folder named by THESEUS_KANBAN_MEASURE")]
public class KanbanMeasureTests
{
    [Test]
    public void TheKanban_OnARealProjectsFolder_LaysOutInTime()
    {
        var folder = Environment.GetEnvironmentVariable("THESEUS_KANBAN_MEASURE");

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Assert.Ignore("Set THESEUS_KANBAN_MEASURE to a projects folder.");
        }

        var settings = new MemorySettingsStore();
        var actions = new ProjectViewActions(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        var watch = Stopwatch.StartNew();
        var readings = ProjectCatalog.Read(folder).Where(reading => reading.IsReadable).ToList();
        var read = watch.Elapsed;

        var sources = readings.Select(reading =>
        {
            var pieces = PiecesOfWork.Build(reading, _ => null, [], []);
            var view = ProjectViewModel.Build(reading, pieces, new LinkStatus([], []), [], settings, actions);

            return new KanbanProjectSource(reading.Name, reading.File!.Title, view);
        }).ToList();
        var views = watch.Elapsed;

        var kanban = KanbanViewModel.Build(sources, settings, _ => null, _ => { }, (_, _) => { });
        var built = watch.Elapsed;

        var control = new KanbanView { DataContext = kanban, Width = 1600, Height = 900 };
        control.Measure(new Size(1600, 900));
        control.Arrange(new Rect(0, 0, 1600, 900));
        control.UpdateLayout();
        var laidOut = watch.Elapsed;

        var again = KanbanViewModel.Build(sources, settings, _ => null, _ => { }, (_, _) => { });
        var warmStart = watch.Elapsed;
        var second = new KanbanView { DataContext = again, Width = 1600, Height = 900 };
        second.Measure(new Size(1600, 900));
        var measured = watch.Elapsed - warmStart;
        second.Arrange(new Rect(0, 0, 1600, 900));
        second.UpdateLayout();
        var warm = watch.Elapsed - warmStart;
        TestContext.Out.WriteLine($"EXPERIMENT warm measure {measured.TotalMilliseconds:F0} ms; visuals {Count(second)}");

        TestContext.Out.WriteLine($"projects {kanban.Rows.Count}, cards {kanban.Cards.Count()}");
        foreach (var row in kanban.Rows)
        {
            TestContext.Out.WriteLine($"  {row.Title}: {row.Cards.Count()} cards ({row.CountsText}); by column "
                + string.Join(" ", row.Cells.Select(cell => $"{cell.Column.Name}={cell.Cards.Count}")));
        }

        TestContext.Out.WriteLine($"read {read.TotalMilliseconds:F0} ms; project views {(views - read).TotalMilliseconds:F0} ms; "
            + $"kanban {(built - views).TotalMilliseconds:F0} ms; layout {(laidOut - built).TotalMilliseconds:F0} ms; "
            + $"kanban and layout {(laidOut - views).TotalMilliseconds:F0} ms");
        var theseus = sources.Single(source => source.Title == "Theseus").View!;
        var pvStart = watch.Elapsed;
        var pv = new ProjectView { DataContext = theseus, Width = 1600, Height = 900 };
        pv.Measure(new Size(1600, 900));
        pv.Arrange(new Rect(0, 0, 1600, 900));
        pv.UpdateLayout();
        TestContext.Out.WriteLine($"EXPERIMENT project view of Theseus {(watch.Elapsed - pvStart).TotalMilliseconds:F0} ms");
        var oneStart = watch.Elapsed;
        var one = KanbanViewModel.Build([sources.Single(source => source.Title == "Theseus")], settings, _ => null, _ => { }, (_, _) => { });
        var oneView = new KanbanView { DataContext = one, Width = 1600, Height = 900 };
        oneView.Measure(new Size(1600, 900));
        oneView.Arrange(new Rect(0, 0, 1600, 900));
        oneView.UpdateLayout();
        TestContext.Out.WriteLine($"EXPERIMENT kanban of Theseus alone {(watch.Elapsed - oneStart).TotalMilliseconds:F0} ms");
        TestContext.Out.WriteLine($"warm kanban and layout {warm.TotalMilliseconds:F0} ms");
        TestContext.Out.WriteLine($"unchanged rebuild has the same signature: {again.Signature == kanban.Signature}");

        static int Count(System.Windows.DependencyObject root)
        {
            var total = 1;
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            {
                total += Count(System.Windows.Media.VisualTreeHelper.GetChild(root, index));
            }

            return total;
        }

        Assert.That(warm.TotalMilliseconds, Is.LessThan(300));
        Assert.That(again.Signature, Is.EqualTo(kanban.Signature));
    }
}
