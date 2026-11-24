using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project view laid out as WPF lays it out (method rule P11), for PR 4 (1.1d AC 45 to 54): grouped by objectives,
/// with headings over the Flow's own boxes and no connector; and in a 720 px window with the Live sessions panel open,
/// where the pane steps aside, Properties opens over the flow, and nothing is cut off.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewGroupingLayoutTests
{
    // What MainWindow leaves the view in a 720 px window with the Live sessions panel open (ProjectViewLayoutTests).
    private const double Narrow = 720 - 16 - 296 - 24;

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

    private static ProjectView LaidOut(ProjectViewModel view, double width = 1400, double height = 900)
    {
        var control = new ProjectView { DataContext = view };

        return Relaid(control, width, height);
    }

    private static ProjectView Relaid(ProjectView control, double width, double height = 900)
    {
        control.Width = width;
        control.Height = height;
        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        control.UpdateLayout();

        return control;
    }

    private static IEnumerable<T> AllNamed<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).Where(element => element.Name == name);

    private static Border BoxOf(DependencyObject root, string piece) =>
        AllNamed<Border>(root, "Box").Single(box => box.DataContext is PieceNodeViewModel node && node.Name == piece);

    [Test]
    public void GroupedByObjectives_PhasesAndObjectivesAreHeadings_OverTheFlowsBoxes()
    {
        var view = _views.BuildYourMarket();
        view.IsGroupedByObjectives = true;
        var control = LaidOut(view);
        var objectives = (FrameworkElement)control.FindName("ObjectiveList");

        Assert.That(AllNamed<TextBlock>(objectives, "PhaseTitle").Select(text => text.Text), Is.EqualTo(new[]
        {
            "Phase 0: Specify", "Phase 1: Build read-only, internal", "Phase 2: Customer release",
        }));
        Assert.That(AllNamed<TextBlock>(objectives, "ObjectiveTitle").Select(text => text.Text), Is.EqualTo(new[]
        {
            "1.1 Preconditions", "1.2 Identity and Config", "1.3 BuildYourMarket.Api, release 2a",
            "1.4 BuildYourMarket.Mcp host and deployment", "1.5 Data protection", "1.6 Staff pilot", "Not in project.md",
        }), "phase 0 is done, so it starts folded and its objective is not laid out");
        Assert.That(AllNamed<TextBlock>(objectives, "NothingYet").Count(text => Shown(text, control)), Is.EqualTo(2));
        Assert.That(AllNamed<Border>(objectives, "Box").Count(), Is.EqualTo(8), "every piece of the open phases once, under its objective");
        Assert.That(AllNamed<Border>((FrameworkElement)control.FindName("RootList"), "Box"), Is.Empty);

        var readApi = BoxOf(objectives, "bym-mcp-read-api");

        Assert.That(AllNamed<TextBlock>(readApi, "CrossMark").Select(text => (text.Text, Shown(text, control))),
            Is.EqualTo(new[] { ($"← handed on from Specify the BuildYourMarket MCP server", true) }));
        Assert.That(Shown(AllNamed<TextBlock>(readApi, "ReportBack").Single(), control), Is.True);
        var criteria = AllNamed<Button>(objectives, "ObjectiveCriteria").First();
        Assert.That((criteria.Content as string)?[2..], Is.EqualTo("0 of 1 criteria met"));
        Assert.That(criteria.Content as string, Does.StartWith("▸").Or.StartWith("▾"), "its chevron says the criteria are listed by opening it (walkthrough row 20)");
        Assert.That(AllNamed<ItemsControl>(objectives, "ObjectiveCounts").First().Items.Cast<StateCountViewModel>().Single().Text,
            Is.EqualTo("✓ 1 answered"), "W0 reported back and you confirmed it (1.1f E29)");
    }

    [Test]
    public void InTheFlow_TheMarksAreShown_SinceNoLineIsDrawn()
    {
        var control = LaidOut(_views.BuildYourMarket());

        Assert.That(AllNamed<ItemsControl>(control, "CrossMarks").Select(marks => marks.Visibility), Has.All.EqualTo(Visibility.Visible),
            "a round's pieces sit under their states' headings, so a box says who handed it work (git-22, 2026-11-24)");
        Assert.That(((ItemsControl)control.FindName("PhaseList")).Items, Is.Empty, "no heading is laid out in the Flow");
        Assert.That(AllNamed<Border>((FrameworkElement)control.FindName("ObjectiveList"), "Box"), Is.Empty);
    }

    [Test]
    public void AHeadingsChevron_FoldsItsBranch()
    {
        var view = _views.Theseus();
        view.IsGroupedByObjectives = true;
        var control = LaidOut(view);
        var phase = AllNamed<Button>(control, "PhaseChevron").First();

        phase.Command.Execute(null);
        Relaid(control, 1400);

        Assert.That(AllNamed<Border>(control, "PhaseBranch").First().Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That(phase.Content, Is.EqualTo("▸"));
    }

    [Test]
    public void SwitchingTheGrouping_StartsAtTheTop()
    {
        var view = _views.BuildYourMarket();
        view.ExpandAllCommand.Execute(null);
        var control = LaidOut(view, 1400, 400);
        var flow = (ScrollViewer)control.FindName("Flow");

        flow.ScrollToVerticalOffset(300);
        Relaid(control, 1400, 400);
        view.IsGroupedByObjectives = true;
        Relaid(control, 1400, 400);
        Pump();

        Assert.That(flow.VerticalOffset, Is.Zero, "a switch lays out other boxes entirely (PR 4 review, finding 3)");
    }

    // Runs what the view queued on the dispatcher (its scrolling), as a running window would.
    private static void Pump() =>
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

    [Test]
    public void FoldedAndOpenBoxes_AreLeftAligned_At290And640_InAWideView()
    {
        var view = _views.Theseus();
        view.Toggle(view.AllNodes.Single(node => node.Name == "project-view"));

        foreach (var run in view.StateGroups.Where(group => !group.IsOpen).ToList())
        {
            run.ToggleCommand.Execute(null);
        }

        var control = LaidOut(view);
        var connectors = (FrameworkElement)control.FindName("FlowContent");
        var folded = BoxOf(control, "method-kit");
        var open = BoxOf(control, "project-view");

        Assert.That((folded.ActualWidth, open.ActualWidth), Is.EqualTo((290.0, 640.0)));
        Assert.That(open.TranslatePoint(new Point(0, 0), connectors).X, Is.EqualTo(folded.TranslatePoint(new Point(0, 0), connectors).X));
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void At720WithLiveSessionsOpen_NothingIsCutOff(bool byObjectives, bool open)
    {
        foreach (var view in new[] { _views.Theseus(), _views.BuildYourMarket() })
        {
            view.IsGroupedByObjectives = byObjectives;
            (open ? view.ExpandAllCommand : view.CollapseAllCommand).Execute(null);
            var control = LaidOut(view, Narrow);
            var flow = (ScrollViewer)control.FindName("Flow");
            // The phase strip scrolls across by design: one line however many phases (walkthrough row 17).
            var strip = (ScrollViewer)control.FindName("PhaseStripScroller");
            var where = $"{view.Name}, {(byObjectives ? "objectives" : "flow")}, {(open ? "open" : "folded")}";

            Assert.That(flow.ExtentWidth, Is.LessThanOrEqualTo(flow.ViewportWidth + 0.5), $"{where}: no sideways scroll");

            foreach (var element in Descendants<FrameworkElement>(control)
                         .Where(element => element is Button or TextBlock or Border { Name: "Box" } && Shown(element, control) && element.ActualWidth > 0
                                           && !strip.IsAncestorOf(element)))
            {
                var inFlow = flow.IsAncestorOf(element);
                var right = element.TranslatePoint(new Point(element.ActualWidth, 0), inFlow ? flow : control).X;

                Assert.That(right, Is.LessThanOrEqualTo((inFlow ? flow.ViewportWidth : Narrow) + 0.5), $"{where}: {Describe(element)} runs off the view");

                if (element is TextBlock text)
                {
                    Assert.That(Fits(text), Is.True, $"{where}: '{text.Text}' is cut off at {text.ActualWidth:0.#} px");
                }
                else if (element is Button button)
                {
                    Assert.That(button.DesiredSize.Width - button.Margin.Left - button.Margin.Right, Is.LessThanOrEqualTo(button.ActualWidth + 0.5),
                        $"{where}: {Describe(button)} is clipped");
                }
            }
        }
    }

    [Test]
    public void At720WithLiveSessionsOpen_ThePaneStepsAside_AndSelectingOpensItOverTheFlow_WhichIsNotChanged()
    {
        var view = _views.BuildYourMarket();
        var control = LaidOut(view, Narrow);
        var flow = (ScrollViewer)control.FindName("Flow");
        var pane = (Border)control.FindName("Pane");
        var box = BoxOf(control, "bym-mcp-read-api");
        var before = (flow.ActualWidth, box.TranslatePoint(new Point(0, 0), flow));

        Assert.That((view.IsPaneDocked, pane.Visibility), Is.EqualTo((false, Visibility.Collapsed)));
        Assert.That(flow.ActualWidth, Is.EqualTo(Narrow).Within(0.5), "the flow takes the whole width");
        Assert.That(((GridSplitter)control.FindName("Splitter")).Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That(_views.Notices, Is.EqualTo(new[] { "Properties hidden: the window is too narrow" }));

        view.Select(view.AllNodes.Single(node => node.Name == "bym-mcp-read-api"));
        Relaid(control, Narrow);
        var left = pane.TranslatePoint(new Point(0, 0), control).X;

        Assert.That((pane.Visibility, pane.ActualWidth), Is.EqualTo((Visibility.Visible, ProjectPaneWidth.Minimum)));
        Assert.That(left + pane.ActualWidth, Is.EqualTo(Narrow).Within(0.5), "on the right of the view");
        Assert.That(Panel.GetZIndex(pane), Is.GreaterThan(Panel.GetZIndex(flow)), "over the flow");
        Assert.That(((TextBlock)control.FindName("PropertiesHeading")).Text, Is.EqualTo("BuildYourMarket.Api, release 2a"), "its title, never a folder name (1.1f H40)");
        Assert.That(((Button)control.FindName("CloseOverlay")).Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That((flow.ActualWidth, box.TranslatePoint(new Point(0, 0), flow)), Is.EqualTo(before), "the flow under it is not changed");
        Assert.That(pane.TranslatePoint(new Point(pane.ActualWidth, 0), control).X, Is.LessThanOrEqualTo(Narrow + 0.5));
    }

    [Test]
    public void At720WithoutTheLiveSessionsPanel_ThePaneDocks_NarrowedToFitBesideTheFlow()
    {
        const double width = 720 - 16 - 24;
        var control = LaidOut(_views.Theseus(), width);
        var pane = (Border)control.FindName("Pane");

        Assert.That(pane.TranslatePoint(new Point(pane.ActualWidth, 0), control).X, Is.EqualTo(width).Within(0.5), "the pane's right edge is the view's");
        Assert.That(((ScrollViewer)control.FindName("Flow")).ActualWidth, Is.EqualTo(ProjectPaneWidth.MinimumFlow).Within(0.5));
        Assert.That(_views.Notices, Is.Empty);
    }

    [Test]
    public void ClosingTheOverlay_LeavesTheSelection_AndWideningDocksThePaneAtItsRememberedWidth()
    {
        var view = _views.Theseus();
        view.RememberPaneWidth(420);
        var control = LaidOut(view, Narrow);
        var box = view.AllNodes.Single(node => node.Name == "stop-timing");
        view.Select(box);
        Relaid(control, Narrow);

        ((Button)control.FindName("CloseOverlay")).Command.Execute(null);
        Relaid(control, Narrow);

        Assert.That(((Border)control.FindName("Pane")).Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That((view.Selection, box.IsSelected), Is.EqualTo((box, true)));

        Relaid(control, 1400);
        var pane = (Border)control.FindName("Pane");

        Assert.That((view.IsPaneDocked, pane.Visibility, Grid.GetColumn(pane)), Is.EqualTo((true, Visibility.Visible, 2)));
        Assert.That(pane.ActualWidth, Is.EqualTo(420), "at its remembered width");
        Assert.That(((Button)control.FindName("CloseOverlay")).Visibility, Is.EqualTo(Visibility.Collapsed));
        Assert.That(((TextBlock)control.FindName("PropertiesHeading")).Text, Is.EqualTo("Five changes through the method by hand"));
    }

    // Shown: it and every element above it are Visible. IsVisible needs a window, and these views are laid out without one.
    private static bool Shown(FrameworkElement element, DependencyObject root)
    {
        for (DependencyObject? at = element; at is not null && at != root; at = VisualTreeHelper.GetParent(at))
        {
            if (at is UIElement { Visibility: not Visibility.Visible })
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(FrameworkElement element) => element switch
    {
        TextBlock text => $"'{text.Text}'",
        Button { Content: string label } => $"button '{label}'",
        _ => $"{element.GetType().Name} {element.Name}",
    };

    // As ProjectViewLayoutTests reads a line: trimmed with an ellipsis is fine, a wrapping line fits when each word does,
    // any other line when the whole of it does.
    private static bool Fits(TextBlock text)
    {
        if (text.Text.Length == 0 || text.TextTrimming != TextTrimming.None)
        {
            return true;
        }

        var parts = text.TextWrapping == TextWrapping.NoWrap ? [text.Text] : text.Text.Split(' ');
        var typeface = new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch);

        return parts.All(part => new FormattedText(part, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, typeface, text.FontSize,
            Brushes.Black, 1.0).Width <= text.ActualWidth + 0.5);
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
