using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;
using static Theseus.Tests.Projects.ScaleFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project view over a project of 35 objectives and no workstreams, laid out as WPF lays it out (project-view-scale,
/// 2026-11-24: "a very big banner of objectives and cant scroll or take any actions"). No test moves keyboard focus or
/// needs a UI Automation client listening: peers are made directly, as CI can (BASELINE section 8).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewScaleLayoutTests
{
    private const double Width = 1400;
    private const double Height = 900;

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

    private ProjectViewModel LargeView() => _views.Large();

    private static ProjectView LaidOut(ProjectView control, double height = Height)
    {
        control.Width = Width;
        control.Height = height;
        control.Measure(new Size(Width, height));
        control.Arrange(new Rect(0, 0, Width, height));
        control.UpdateLayout();

        return control;
    }

    [Test]
    public void InAShortWindow_TheSummaryScrollsWithinItsShare_AndTheWorkStillShows()
    {
        const double Short = 400;
        var control = LaidOut(new ProjectView { DataContext = LargeView() }, Short);
        var scroller = Named<ScrollViewer>(control, "WhereScroller");

        Assert.That(scroller.ActualHeight, Is.LessThanOrEqualTo(WhereShare.MaxHeight(Short) + 0.5));
        Assert.That(scroller.ScrollableHeight, Is.GreaterThan(0), "the summary is taller than its share here, so it scrolls");
        Assert.That(Named<ScrollViewer>(control, "Flow").ActualHeight, Is.GreaterThan(0), "the work is still on the page");
    }

    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).First(element => element.Name == name);

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

    [Test]
    public void ALargeProject_KeepsTheSummaryWithinItsShare_AndLeavesTheWorkRoomToScroll()
    {
        var control = LaidOut(new ProjectView { DataContext = LargeView() });
        var where = Named<Border>(control, "Where");
        var flow = Named<ScrollViewer>(control, "Flow");

        Assert.That(where.ActualHeight, Is.LessThanOrEqualTo(Height * WhereShare.Share + 1), "the summary takes no more than its share");
        Assert.That(flow.ActualHeight, Is.GreaterThan(Height / 3), "the work keeps room under the summary and toolbar");
        Assert.That(flow.ScrollableHeight, Is.GreaterThan(0), "35 objectives are more than a window, and the page scrolls to them");
    }

    [Test]
    public void TheBand_ShowsAFewLines_ThenAndMore()
    {
        var view = LargeView();
        var control = LaidOut(new ProjectView { DataContext = view });
        var more = Named<Button>(control, "MoreWaits");

        Assert.That(Named<ItemsControl>(control, "WaitItems").Items, Has.Count.EqualTo(ProjectViewModel.ShownWaitsLimit));
        Assert.That((more.Visibility, view.MoreWaitsText), Is.EqualTo((Visibility.Visible, $"and {Yours - ProjectViewModel.ShownWaitsLimit} more…")));
        Assert.That(more.Command.CanExecute(null), Is.True);
    }

    [Test]
    public void AndMore_IsNotShown_WhenEveryLineFits()
    {
        WriteBuildYourMarket(_folder);
        var control = LaidOut(new ProjectView { DataContext = _views.BuildYourMarket() });

        Assert.That(Named<Button>(control, "MoreWaits").Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void TheLeadsLine_SitsUnderTheBand_SayingWhatTheLeadPlans()
    {
        var control = LaidOut(new ProjectView { DataContext = LargeView() });
        var line = Named<TextBlock>(control, "LeadWaitsLine");

        Assert.That((line.Visibility, line.Text), Is.EqualTo((Visibility.Visible, "Waits on the lead, large-lead: 35 objectives have no workstream yet.")));
    }

    [Test]
    public void AnotherProjectsTab_ShowsItsOwnWorkAndCounts_ToAnAutomationReadToo()
    {
        WriteBuildYourMarket(_folder);
        var bym = _views.BuildYourMarket(_views.BuildYourMarket());
        var control = LaidOut(new ProjectView { DataContext = bym });
        var counts = UIElementAutomationPeer.CreatePeerForElement(Named<ItemsControl>(control, "Counts"));
        var rounds = UIElementAutomationPeer.CreatePeerForElement(Named<ItemsControl>(control, "RootList"));
        Assert.That(rounds.GetChildren(), Is.Not.Empty, "BuildYourMarket's rounds are read first");

        var large = LargeView();
        control.DataContext = large;
        LaidOut(control);
        control.ForgetAutomationChildren();

        Assert.That(Descendants<FrameworkElement>(Named<ItemsControl>(control, "RootList")).Select(element => element.DataContext).OfType<RoundViewModel>(),
            Is.Empty, "no BuildYourMarket round is drawn behind the large project's tab");
        Assert.That(rounds.GetChildren() ?? [], Is.Empty);
        Assert.That(counts.GetChildren().OfType<ItemAutomationPeer>().Select(peer => ((StateCountViewModel)peer.Item).Text),
            Is.EqualTo(large.Counts.Select(chip => chip.Text)));
    }

    [Test]
    public void TheChecklist_ListsEveryWait_GroupedWithItsCriteria_AndCloses()
    {
        var view = LargeView();
        WaitsChecklistViewModel? opened = null;
        view.AllWaitsRequested += (_, list) => opened = list;

        view.ShowAllWaitsCommand.Execute(null);
        var window = Theseus.Tests.Windows.DialogHost.Open(new WaitsWindow(opened!));
        var all = Yours + view.OtherWaits.Count + view.Phases.Sum(phase => phase.Held.Count);

        Assert.That(((ItemsControl)window.FindName("Rows")).Items, Has.Count.EqualTo(all));
        Assert.That(((TextBlock)window.FindName("Count")).Text, Is.EqualTo($"0 of {all} selected"));
        Assert.That(opened!.Groups[0].Members, Has.Count.EqualTo(10), "the shared ask lists the ten criteria it covers");
        Assert.That(((Button)window.FindName("CloseButton")).IsCancel, Is.True);
        window.Close();
    }

    [Test]
    public void TheOthersLine_SaysWhomTheyAreOn_WithRespond()
    {
        var control = LaidOut(new ProjectView { DataContext = LargeView() });

        Assert.That(Named<TextBlock>(control, "OthersText").Text, Is.EqualTo($"Waits on others: 2, on {Team} and {LegacyTeam}."));
        Assert.That(Named<Button>(control, "RespondToOthers").Command.CanExecute(null), Is.True);
    }
}
