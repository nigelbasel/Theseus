using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The tab's amber dot as WPF lays it out (board-small-changes item 2): shown while its project has waits, hidden when
/// not, and the tab's automation name carries the count, so a screen reader hears what the colour shows.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectTabWaitsDotLayoutTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ListBoxItem LaidOutTab(int waits)
    {
        var reading = WriteTheseus(_folder);
        var view = ProjectViewModel.Build(reading, TheseusPieces(_folder), LinksOn([], []),
            ProjectTabViewModel.For([reading], TheseusProject, _ => null, waitsOf: _ => waits), _views.Settings, _views.Actions);
        var strip = new ProjectTabStrip { DataContext = new TabStripContext(view.Tabs), Width = 1400 };

        strip.Measure(new Size(1400, 60));
        strip.Arrange(new Rect(0, 0, 1400, 60));
        strip.UpdateLayout();

        return Descendants<ListBoxItem>(strip).Single(item => item.DataContext is ProjectTabViewModel);
    }

    [Test]
    public void ATabWithWaits_ShowsItsDot_AndIsNamedWithTheCount()
    {
        var tab = LaidOutTab(3);
        var dot = Descendants<Ellipse>(tab).Single(ellipse => ellipse.Name == "WaitsDot");

        Assert.That(dot.Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(AutomationProperties.GetName(tab), Is.EqualTo("Theseus bootstrap, 3 waiting for you"));
    }

    [Test]
    public void ATabWithNoWaits_HidesItsDot()
    {
        var tab = LaidOutTab(0);

        Assert.That(Descendants<Ellipse>(tab).Single(ellipse => ellipse.Name == "WaitsDot").Visibility, Is.EqualTo(Visibility.Collapsed));
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

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
