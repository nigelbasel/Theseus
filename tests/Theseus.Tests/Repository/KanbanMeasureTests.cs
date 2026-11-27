using System.Diagnostics;
using Theseus.App.ViewModels;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Tests.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// The kanban built over a real projects folder (projects-kanban spec AC 26): its rows and cards per column, and the time
/// to build it. It runs only when <c>THESEUS_KANBAN_MEASURE</c> names a folder, so no machine path lives in the repo. The
/// projects are read, never written. Layout is timed in the running app, where the theme's resources are loaded; this
/// host lays out the project view itself several times slower than the app does.
/// </summary>
[TestFixture]
[Explicit("Measures a real projects folder named by THESEUS_KANBAN_MEASURE")]
public class KanbanMeasureTests
{
    [Test]
    public void TheKanban_OnARealProjectsFolder_IsBuiltInTime_AndUnchangedGivesTheSameSignature()
    {
        var folder = Environment.GetEnvironmentVariable("THESEUS_KANBAN_MEASURE");

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            Assert.Ignore("Set THESEUS_KANBAN_MEASURE to a projects folder.");
        }

        var settings = new MemorySettingsStore();
        var actions = new ProjectViewActions(_ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { }, _ => { });
        var readings = ProjectCatalog.Read(folder).Where(reading => reading.IsReadable).ToList();
        var watch = Stopwatch.StartNew();
        var sources = readings.Select(reading => new KanbanProjectSource(reading.Name, reading.File!.Title,
            ProjectViewModel.Build(reading, PiecesOfWork.Build(reading, _ => null, [], []), new LinkStatus([], []), [], settings, actions))).ToList();
        var views = watch.Elapsed;
        var kanban = KanbanViewModel.Build(sources, settings, _ => null, _ => { }, (_, _) => { });
        var built = watch.Elapsed - views;
        var again = KanbanViewModel.Build(sources, settings, _ => null, _ => { }, (_, _) => { });

        TestContext.Out.WriteLine($"projects {kanban.Rows.Count}, cards {kanban.Cards.Count()}");

        foreach (var row in kanban.Rows)
        {
            TestContext.Out.WriteLine($"  {row.Title}: {row.Cards.Count()} cards ({row.CountsText}); "
                + string.Join(" ", row.Cells.Select(cell => $"{cell.Column.Name}={cell.Cards.Count}")));
        }

        TestContext.Out.WriteLine($"project views {views.TotalMilliseconds:F0} ms; kanban {built.TotalMilliseconds:F0} ms");

        Assert.That(built.TotalMilliseconds, Is.LessThan(300));
        Assert.That(again.Signature, Is.EqualTo(kanban.Signature));
    }
}
