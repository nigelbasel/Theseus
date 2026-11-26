using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// A project's own colour carried into its view (1.1d AC 40): each tab's mark and the summary's mark take the project's
/// colour, which PR 2 drew in the accent colour until this.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewColourTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheTabsAndTheSummaryMark_TakeEachProjectsOwnColour()
    {
        var reading = WriteTheseus(_folder);
        WriteBuildYourMarket(_folder);
        IReadOnlyList<PieceCard> cards = [Card(Git22, SessionLifecycle.Idle)];
        var pieces = PiecesOfWork.Build(reading, _ => null, cards, []);
        var colours = new Dictionary<string, string> { [TheseusProject] = "#8674D6", [BuildYourMarket] = "#1D9F5E" };
        var tabs = ProjectTabViewModel.For(ProjectCatalog.Read(_folder.Folder), TheseusProject, _ => null, name => colours[name]);
        var view = ProjectViewModel.Build(reading, pieces, LinksOn([], cards), tabs, new MemorySettingsStore(),
            new ProjectViews(_folder).Actions);
        var control = new ProjectView { DataContext = view, Width = 1400, Height = 900 };

        control.Measure(new Size(1400, 900));
        control.Arrange(new Rect(0, 0, 1400, 900));
        control.UpdateLayout();

        Assert.That(view.ColourHex, Is.EqualTo("#8674D6"));
        Assert.That(ColourOf((StackedCardMark)control.FindName("SummaryMark")), Is.EqualTo("#FF8674D6"));
        var strip = new ProjectTabStrip { DataContext = new TabStripContext(view.Tabs), Width = 1400 };

        strip.Measure(new Size(1400, 60));
        strip.Arrange(new Rect(0, 0, 1400, 60));
        strip.UpdateLayout();
        Assert.That(Descendants<ListBoxItem>(strip).Where(tab => tab.DataContext is ProjectTabViewModel)
                .Select(tab => (((ProjectTabViewModel)tab.DataContext).Name, ColourOf(Descendants<StackedCardMark>(tab).Single()))),
            Is.EquivalentTo(new[] { (TheseusProject, "#FF8674D6"), (BuildYourMarket, "#FF1D9F5E") }));
    }

    [Test]
    public void AViewWhoseTabsHaveNoColour_KeepsTheAccentColour()
    {
        var view = new ProjectViews(_folder).Theseus();

        Assert.That(view.ColourHex, Is.Null);
        Assert.That(view.Tabs.Select(tab => tab.ColourHex), Is.All.Null);
    }

    private static string ColourOf(StackedCardMark mark) => (mark.Foreground as SolidColorBrush)?.Color.ToString() ?? "none";

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
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
