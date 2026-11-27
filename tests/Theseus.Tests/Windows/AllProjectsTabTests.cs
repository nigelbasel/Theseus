using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Windows;

/// <summary>
/// The kanban in the main window's All projects tab (projects-kanban spec AC 1): with the tab open, the pane draws the
/// shell's kanban through the app's own template, with the strip's All projects tab chosen.
/// </summary>
[TestFixture]
public class AllProjectsTabTests
{
    [Test]
    public void WithAllProjectsOpen_TheMainAreaDrawsTheKanban_AndTheStripChoosesItsTab()
    {
        using var folder = TestProjectsFolder.Create();
        var views = new ProjectViews(folder);

        MainWindowFixture.Run((window, shell) =>
        {
            var kanban = KanbanViewModel.Build(
                [
                    new KanbanProjectSource(TheseusProject, "Theseus", views.Theseus()) { WaitingCount = 2 },
                    new KanbanProjectSource(BuildYourMarket, "BuildYourMarket MCP", views.BuildYourMarket()),
                ],
                views.Settings, _ => null, _ => { }, (_, _) => { });

            shell.ShowAllProjects(kanban, [new ProjectTabViewModel(TheseusProject, "Theseus", false, null)]);
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1400, 760));
            content.Arrange(new Rect(0, 0, 1400, 760));
            content.UpdateLayout();

            var pane = (ContentControl)window.FindName("AllProjectsPane");
            var drawn = Descendants<KanbanView>(pane).SingleOrDefault();

            Assert.Multiple(() =>
            {
                Assert.That(pane.Visibility, Is.EqualTo(Visibility.Visible));
                Assert.That(drawn?.DataContext, Is.SameAs(kanban), "the app's template draws the shell's kanban");
                Assert.That(drawn?.ActualWidth, Is.GreaterThan(0));
                Assert.That(shell.OpenProjectTab?.IsAllProjects, Is.True);
            });

            if (Environment.GetEnvironmentVariable("THESEUS_KANBAN_SCREENSHOT") is { Length: > 0 } path)
            {
                Save(content, path);
            }
        });
    }

    // A picture of the window as laid out, for the record of the change; written only where the variable names.
    private static void Save(FrameworkElement content, string path)
    {
        var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));

        using var file = File.Create(path);
        png.Save(file);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var each in Descendants<T>(child))
            {
                yield return each;
            }
        }
    }
}
