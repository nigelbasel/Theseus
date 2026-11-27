using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Kanban;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The projects kanban as WPF lays it out (projects-kanban spec AC 5, 11, 13, 15, 24): cells as wide as their columns,
/// a collapsed column as its strip, the row headers held at the left while the body scrolls sideways, a fit that follows
/// the cards, and every card named for screen readers.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class KanbanViewLayoutTests
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

    private (KanbanView View, KanbanViewModel Kanban) LaidOut(double width = 1000)
    {
        var kanban = KanbanViewModel.Build(
            [
                new KanbanProjectSource(TheseusProject, "Theseus", _views.Theseus()) { WaitingCount = 2 },
                new KanbanProjectSource(ProjectViewFixtures.BuildYourMarket, "BuildYourMarket MCP", _views.BuildYourMarket()),
            ],
            _views.Settings, _ => null, _ => { }, (_, _) => { });
        var view = new KanbanView { DataContext = kanban, Width = width, Height = 800 };

        Lay(view);

        return (view, kanban);
    }

    private static void Lay(FrameworkElement view)
    {
        view.Measure(new Size(view.Width, view.Height));
        view.Arrange(new Rect(0, 0, view.Width, view.Height));
        view.UpdateLayout();
    }

    [Test]
    public void EveryCell_IsAsWideAsItsColumn_AndACollapsedColumnIsAStrip()
    {
        var (view, _) = LaidOut();
        var cells = Descendants<ContentPresenter>(view).Where(presenter => presenter.Content is KanbanCellViewModel).ToList();

        Assert.That(cells, Is.Not.Empty);

        foreach (var cell in cells)
        {
            var model = (KanbanCellViewModel)cell.Content;

            Assert.That(cell.ActualWidth, Is.EqualTo(model.Column.Width).Within(0.5), model.AutomationName);
        }

        Assert.That(cells.Where(cell => ((KanbanCellViewModel)cell.Content).Column.Column == KanbanColumn.Done)
            .Select(cell => cell.ActualWidth), Is.All.EqualTo(KanbanLayout.CollapsedWidth).Within(0.5));
    }

    [Test]
    public void TheRowHeaders_StayAtTheLeft_WhenTheBodyScrollsSideways()
    {
        var (view, _) = LaidOut(width: 700);
        var body = (ScrollViewer)view.FindName("Body");

        Assert.That(body.ScrollableWidth, Is.GreaterThan(0), "eight columns are wider than 700 px");

        body.ScrollToHorizontalOffset(300);
        Lay(view);

        var header = Descendants<Border>(view).First(border => border.Name == "RowHeader");

        Assert.That(header.TranslatePoint(new Point(0, 0), view).X, Is.EqualTo(0).Within(0.5));
    }

    [Test]
    public void ResizingAColumn_WidensItsCells()
    {
        var (view, kanban) = LaidOut();
        var plan = kanban.Columns.Single(column => column.Column == KanbanColumn.Plan);

        plan.Resize(400);
        Lay(view);

        var cell = Descendants<ContentPresenter>(view).First(presenter => presenter.Content is KanbanCellViewModel model && model.Column == plan);

        Assert.That(cell.ActualWidth, Is.EqualTo(400).Within(0.5));
    }

    [Test]
    public void AtItsNarrowest_AColumnHoldsItsCardsWhole()
    {
        var (view, kanban) = LaidOut();

        foreach (var column in kanban.Columns)
        {
            column.Resize(0);
        }

        Lay(view);

        var frames = Descendants<Border>(view).Where(border => border.Name == "CardFrame" && IsShown(border)).ToList();

        Assert.That(frames, Is.Not.Empty);

        foreach (var frame in frames)
        {
            var model = (KanbanCardViewModel)frame.DataContext;
            var cell = Descendants<ContentPresenter>(view).First(presenter => presenter.Content is KanbanCellViewModel each && each.Cards.Contains(model));

            Assert.That(frame.ActualWidth + 17, Is.LessThanOrEqualTo(cell.ActualWidth + 0.5), model.Title);
        }
    }

    [Test]
    public void Fitting_FollowsTheWidestCard_WithinTheBounds()
    {
        var (view, kanban) = LaidOut();
        var column = kanban.Columns.First(each => each.IsOpen && kanban.Cards.Any(card => card.Column == each.Column));

        var widest = view.WidestCard(column);

        Assert.That(widest, Is.GreaterThan(0));

        column.Fit(widest);

        Assert.That(column.Width, Is.EqualTo(KanbanLayout.Clamp(widest)));
    }

    [Test]
    public void EveryCard_IsNamedForScreenReaders()
    {
        var (view, kanban) = LaidOut();
        var frames = Descendants<Border>(view).Where(border => border.Name == "CardFrame").ToList();

        Assert.That(frames.Select(AutomationProperties.GetName),
            Is.EquivalentTo(kanban.Cards.Where(card => kanban.Columns.Single(column => column.Column == card.Column).IsOpen)
                .Select(card => card.AutomationName)));
    }

    [Test]
    public void AFoldedRow_ShowsOnlyCounts()
    {
        var (view, kanban) = LaidOut();

        kanban.Rows[0].IsFolded = true;
        Lay(view);

        var firstRow = Descendants<ContentPresenter>(view).First(presenter => presenter.Content == kanban.Rows[0]);

        var secondRow = Descendants<ContentPresenter>(view).First(presenter => presenter.Content == kanban.Rows[1]);

        Assert.That(Descendants<Border>(firstRow).Count(border => border.Name == "CardFrame" && IsShown(border)), Is.Zero);
        Assert.That(Descendants<Border>(secondRow).Count(border => border.Name == "CardFrame" && IsShown(border)), Is.Positive);
    }

    // Out of a window nothing is IsVisible, and a collapsed element keeps its last width: a card is shown when layout
    // gave it a width and nothing it sits in is collapsed or hidden.
    private static bool IsShown(FrameworkElement element)
    {
        for (DependencyObject? each = element; each is not null; each = VisualTreeHelper.GetParent(each))
        {
            if (each is UIElement { Visibility: not Visibility.Visible })
            {
                return false;
            }
        }

        return element.ActualWidth > 0;
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
