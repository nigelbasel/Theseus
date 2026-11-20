using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project view's own control, laid out as WPF lays it out (method rule P11), without the application's resources:
/// the title bar and tabs, the summary, the boxes folded and open, the branch, the connectors placed from the boxes'
/// layout, the tab order, and widths that clip nothing (1.1d AC 16, 17, 19, 20, 23 to 27, 30, 31).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewLayoutTests
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

    private static ProjectView LaidOut(ProjectViewModel view, double width = 1400, double height = 900)
    {
        var control = new ProjectView { DataContext = view, Width = width, Height = height };

        control.Measure(new Size(width, height));
        control.Arrange(new Rect(0, 0, width, height));
        control.UpdateLayout();

        return control;
    }

    private static T Named<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).First(element => element.Name == name);

    private static IEnumerable<T> AllNamed<T>(DependencyObject root, string name) where T : FrameworkElement =>
        Descendants<T>(root).Where(element => element.Name == name);

    private static Border BoxOf(ProjectView control, string piece) =>
        AllNamed<Border>(control, "Box").Single(box => box.DataContext is PieceNodeViewModel node && node.Name == piece);

    [Test]
    public void TheTitleBar_StartsWithBackToTheBoard_ThenATabPerProject()
    {
        WriteBuildYourMarket(_folder);
        var control = LaidOut(_views.Theseus());
        var back = (Button)control.FindName("BackButton");
        var tabs = Descendants<Button>(control).Where(button => button.DataContext is ProjectTabViewModel).ToList();

        Assert.That(Descendants<Button>(control).First(), Is.SameAs(back), "the first control, and the first Tab stop");
        Assert.That((back.Content, back.ToolTip), Is.EqualTo(("← Board", "Back to the board (Alt+Left)")));
        Assert.That(tabs.Select(tab => ((ProjectTabViewModel)tab.DataContext).Title), Is.EquivalentTo(new[]
        {
            "Theseus bootstrap", "BuildYourMarket MCP server",
        }));
        Assert.That(tabs.Select(tab => Named<Rectangle>(tab, "Underline").Visibility == Visibility.Visible),
            Is.EquivalentTo(new[] { true, false }), "only the open tab is underlined");
    }

    [Test]
    public void TheStackedCardMark_IsThreeOffsetOutlines_18By14()
    {
        var control = LaidOut(_views.Theseus());
        var mark = Descendants<Canvas>(Named<Border>(control, "Where")).First();
        var outlines = mark.Children.OfType<Rectangle>().ToList();

        Assert.That((mark.Width, mark.Height), Is.EqualTo((18.0, 14.0)));
        Assert.That(outlines.Select(each => (Canvas.GetLeft(each), Canvas.GetTop(each), each.Width, each.Height)), Is.EqualTo(new[]
        {
            (4.0, 0.0, 14.0, 10.0), (2.0, 2.0, 14.0, 10.0), (0.0, 4.0, 14.0, 10.0),
        }));
    }

    [Test]
    public void TheSummary_ShowsTitleTagSentenceSixChips_AndTheWaitingBand()
    {
        var view = _views.Theseus();
        var control = LaidOut(view);
        var title = (TextBlock)control.FindName("ProjectTitle");
        var tag = (TextBlock)control.FindName("ProjectTag");
        var chips = Descendants<TextBlock>((ItemsControl)control.FindName("Counts")).Select(text => text.Text);
        var band = (Border)control.FindName("WaitingBand");

        Assert.That((title.Text, title.FontSize, title.FontWeight), Is.EqualTo(("Theseus bootstrap", 20.0, FontWeights.SemiBold)));
        Assert.That((tag.Text, tag.FontSize), Is.EqualTo(("PROJECT", 11.0)));
        Assert.That(((TextBlock)control.FindName("Sentence")).Text, Is.EqualTo(view.Sentence));
        Assert.That(chips, Is.EqualTo(view.Counts.Select(chip => chip.Text)));
        Assert.That(band.Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(Descendants<Button>(band).Select(button => button.Content), Has.Exactly(3).Items);
        Assert.That(((Border)LaidOut(_views.BuildYourMarket()).FindName("WaitingBand")).Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void TheFlow_IsAScrollViewer_HoldingTheConnectorsBehindTheRoots()
    {
        var control = LaidOut(_views.Theseus());
        var flow = (ScrollViewer)control.FindName("Flow");
        var content = (Grid)control.FindName("FlowContent");
        var frame = (Border)control.FindName("FlowFrame");

        Assert.That((flow.HorizontalScrollBarVisibility, flow.VerticalScrollBarVisibility),
            Is.EqualTo((ScrollBarVisibility.Auto, ScrollBarVisibility.Auto)));
        Assert.That(content.Children[0], Is.SameAs(control.FindName("Connectors")));
        Assert.That(((Canvas)content.Children[0]).IsHitTestVisible, Is.False);
        Assert.That(content.Children[1], Is.SameAs(control.FindName("RootList")));
        Assert.That(frame.Padding, Is.EqualTo(new Thickness(20)));
        Assert.That(frame.MaxWidth, Is.EqualTo(flow.ViewportWidth), "the flow is never wider than what shows of it");
    }

    [Test]
    public void RootsStackDown_14PxApart_LeftAligned()
    {
        var control = LaidOut(_views.Theseus());
        var first = BoxOf(control, "method-kit");
        var second = BoxOf(control, "holdout-scenarios");
        var connectors = (Canvas)control.FindName("Connectors");
        var a = first.TranslatePoint(new Point(0, 0), connectors);
        var b = second.TranslatePoint(new Point(0, 0), connectors);

        Assert.That(b.X, Is.EqualTo(a.X));
        Assert.That(b.Y - (a.Y + first.ActualHeight), Is.EqualTo(14).Within(0.5));
    }

    [Test]
    public void AFoldedBox_Is290Wide_WithItsStripeAndAStripOfSevenChips()
    {
        var control = LaidOut(_views.Theseus());
        var box = BoxOf(control, "stop-timing");
        var chips = AllNamed<Grid>(box, "Chip").ToList();

        Assert.That((box.ActualWidth, box.BorderThickness.Left, box.CornerRadius.TopLeft), Is.EqualTo((290.0, 5.0, 7.0)));
        Assert.That(((Button)Named<Button>(box, "Chevron")).Content, Is.EqualTo("▸"));
        Assert.That(Named<TextBlock>(box, "PieceName").TextTrimming, Is.EqualTo(TextTrimming.CharacterEllipsis));
        Assert.That(chips.Select(chip => Descendants<TextBlock>(chip).Single().Text), Is.EqualTo(new[] { "Res", "Spe", "Pla", "Imp", "Ver", "Rev", "Com" }));
        Assert.That(chips.Select(chip => chip.ActualHeight), Has.All.EqualTo(18));
        Assert.That(chips[0].ToolTip, Is.EqualTo("Research: Done"));
        Assert.That(Named<ItemsControl>(box, "Chain").Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void AnOpenBox_Is640Wide_WithItsChainOfStagesJoinedByArrows_AndItsActions()
    {
        var view = _views.Theseus();
        view.ExpandAllCommand.Execute(null);
        var control = LaidOut(view);
        var box = BoxOf(control, "projects-and-method-ui");
        var arrows = AllNamed<TextBlock>(box, "Arrow").Where(arrow => arrow.Visibility == Visibility.Visible).ToList();

        Assert.That(box.ActualWidth, Is.EqualTo(640));
        Assert.That(AllNamed<Button>(box, "Stage").Count(), Is.EqualTo(7));
        Assert.That(arrows.Select(arrow => arrow.Text), Is.EqualTo(Enumerable.Repeat("▶", 6)));
        Assert.That(Descendants<Button>(Named<ItemsControl>(box, "Actions")).Select(button => ((PieceActionViewModel)button.DataContext).Label), Is.EqualTo(new[]
        {
            "Open spec", "Accept spec…", "Verify with holdouts…", "⧉ Focus git-22", "■ Stop", "Open records",
        }));
        Assert.That(Named<ItemsControl>(box, "Strip").Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void TheBranch_IsIndented_WithAThickerLabelledBarWhenParallel()
    {
        var control = LaidOut(_views.BuildYourMarket());
        var branches = AllNamed<Border>(control, "Branch").Where(branch => branch.Visibility == Visibility.Visible).ToList();
        var parallel = branches.Single(branch => ((PieceNodeViewModel)branch.DataContext).Name == ServerSpec);
        var single = branches.Single(branch => ((PieceNodeViewModel)branch.DataContext).Name == "bym-mcp-identity");
        var label = AllNamed<TextBlock>(parallel, "ParallelLabel").Single(text => text.DataContext == parallel.DataContext);

        Assert.That((parallel.Margin.Left, parallel.Padding.Left, parallel.BorderThickness.Left), Is.EqualTo((34.0, 16.0, 3.0)));
        Assert.That(single.BorderThickness.Left, Is.EqualTo(2));
        Assert.That((label.Text, label.Visibility), Is.EqualTo(("7 in parallel", Visibility.Visible)));
        Assert.That(((RotateTransform)label.LayoutTransform).Angle, Is.EqualTo(-90));
        Assert.That(AllNamed<TextBlock>(single, "ParallelLabel").Single(text => text.DataContext == single.DataContext).Visibility,
            Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void TheHandOverConnectors_EndOnTheChildrensLeftEdges_WithinAPixel()
    {
        var view = _views.BuildYourMarket();
        var control = LaidOut(view);
        control.DrawConnectors();
        var connectors = (Canvas)control.FindName("Connectors");
        var root = view.Roots.Single();

        foreach (var child in root.Children)
        {
            var arrow = connectors.Children.OfType<Path>().Single(path => (string)path.Tag == $"HandsOver:{child.Name}:arrow");
            var box = BoxOf(control, child.Name);
            var left = box.TranslatePoint(new Point(0, 0), connectors);

            Assert.That(arrow.Data.Bounds.Right, Is.EqualTo(left.X).Within(1), child.Name);
            Assert.That(arrow.Data.Bounds.Top + 3.5, Is.EqualTo(left.Y + 16).Within(1), child.Name);
        }

        Assert.That(root.Children, Has.Count.EqualTo(7));
        var lines = connectors.Children.OfType<Path>().Where(path => ((string)path.Tag).StartsWith("HandsOver", StringComparison.Ordinal)
                                                                    && !((string)path.Tag).EndsWith(":arrow", StringComparison.Ordinal))
            .Select(path => path.StrokeThickness).ToList();

        Assert.That(lines, Has.Count.EqualTo(8), "seven from the spec, one from identity");
        Assert.That(lines, Has.All.EqualTo(2));
    }

    [Test]
    public void ThenWorkedOn_IsDotted_InUnitsOfItsStroke()
    {
        var control = LaidOut(_views.Theseus());
        control.DrawConnectors();
        var lines = ((Canvas)control.FindName("Connectors")).Children.OfType<Path>()
            .Where(path => ((string)path.Tag).StartsWith("ThenWorkedOn", StringComparison.Ordinal) && !((string)path.Tag).EndsWith(":arrow", StringComparison.Ordinal))
            .ToList();

        Assert.That(lines, Has.Count.EqualTo(11));
        Assert.That(lines.Select(line => (line.StrokeThickness, string.Join(" ", line.StrokeDashArray), line.StrokeDashCap)),
            Has.All.EqualTo((1.5, "1.33 2.67", PenLineCap.Flat)));
    }

    [Test]
    public void TabReachesTheChevron_ThenTheName_ThenTheStages_ThenTheActions_InReadingOrder()
    {
        var view = _views.Theseus();
        view.ExpandAllCommand.Execute(null);
        var box = BoxOf(LaidOut(view), "projects-and-method-ui");

        var stops = Descendants<FrameworkElement>(box).Where(element => element.Focusable && KeyboardNavigation.GetIsTabStop(element))
            .Select(element => element.Name).ToList();

        Assert.That(stops, Is.EqualTo(new[] { "Chevron", "PieceHeader" }.Concat(Enumerable.Repeat("Stage", 7))
            .Concat(Enumerable.Repeat("Action", 6))));
    }

    [Test]
    public void AHeader_SelectsItsPieceWithEnterOrSpace()
    {
        var view = _views.Theseus();
        var header = AllNamed<Border>(LaidOut(view), "PieceHeader").First();
        var keys = header.InputBindings.OfType<KeyBinding>().ToList();

        Assert.That(keys.Select(binding => ((KeyGesture)binding.Gesture).Key), Is.EquivalentTo(new[] { Key.Enter, Key.Space }));

        keys[0].Command.Execute(null);

        Assert.That(view.Selection, Is.SameAs(header.DataContext));
    }

    [Test]
    public void AClickInTheView_PutsFocusInIt_SoItsEscReachesIt()
    {
        var view = _views.Theseus();
        var control = new ProjectView { DataContext = view };
        var button = new Button();
        var window = new Window
        {
            Content = new DockPanel { Children = { button, control } }, Width = 1400, Height = 900, Left = -20000, Top = -20000,
            ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None,
        };

        try
        {
            window.Show();
            button.Focus();
            var name = AllNamed<TextBlock>(control, "PieceName").First();

            name.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = Mouse.PreviewMouseDownEvent });

            var focused = FocusManager.GetFocusedElement(window) as DependencyObject;
            Assert.That(focused, Is.Not.Null);
            Assert.That(focused == control || control.IsAncestorOf(focused!), Is.True, "focus is in the view, not left on the board");
        }
        finally
        {
            window.Close();
        }
    }

    [Test]
    public void ThePropertiesPane_StartsAt320_BesideASixPixelSplitter()
    {
        var control = LaidOut(_views.Theseus());

        Assert.That(((Border)control.FindName("Pane")).ActualWidth, Is.EqualTo(320));
        Assert.That(((GridSplitter)control.FindName("Splitter")).ActualWidth, Is.EqualTo(6));
        Assert.That(((TextBlock)control.FindName("PropertiesHeading")).Text, Is.EqualTo("Properties"));
    }

    [Test]
    public void ThePaneShowsTheSelection_WithWrappedValues()
    {
        var view = _views.Theseus();
        view.Select(view.AllNodes.Single(node => node.Name == "project-view"));
        var control = LaidOut(view);
        var rows = Descendants<TextBlock>((ItemsControl)control.FindName("PropertyRows")).ToList();

        Assert.That(((TextBlock)control.FindName("PropertiesHeading")).Text, Is.EqualTo("project-view"));
        Assert.That(rows.Where(text => Grid.GetColumn(text) == 1).Select(text => text.TextWrapping), Has.All.EqualTo(TextWrapping.Wrap));
        Assert.That(((StackPanel)control.FindName("ObjectiveSection")).Visibility, Is.EqualTo(Visibility.Visible));
        Assert.That(Descendants<Button>((ItemsControl)control.FindName("Criteria")).Single().Content, Is.EqualTo("☐"));
    }

    // The width MainWindow leaves the view in a window this wide: the window's frame (about 16 px), the Live sessions
    // panel when open (280 px and its 16 px margin), the main area's margins (8 + 8) and the view's own (4 + 4).
    private static double ViewWidthIn(double window, bool sessionsPanel) => window - 16 - (sessionsPanel ? 296 : 0) - 24;

    [TestCase(1280, true, true, Description = "a 1280 px window with the Live sessions panel open, every box open")]
    [TestCase(720, false, true, Description = "the minimum window width, every box open")]
    [TestCase(1100, true, false, Description = "an 1100 px window with the Live sessions panel open, every box folded")]
    public void NothingIsClipped_AndThereIsNoHorizontalScrollBar(double window, bool sessionsPanel, bool open)
    {
        var width = ViewWidthIn(window, sessionsPanel);

        foreach (var view in new[] { _views.Theseus(), _views.BuildYourMarket() })
        {
            (open ? view.ExpandAllCommand : view.CollapseAllCommand).Execute(null);
            var control = LaidOut(view, width, 900);
            var flow = (ScrollViewer)control.FindName("Flow");

            Assert.That(flow.ExtentWidth, Is.LessThanOrEqualTo(flow.ViewportWidth + 0.5), $"{view.Name}: no sideways scroll in a {window} px window");

            foreach (var box in AllNamed<Border>(control, "Box"))
            {
                var bounds = new Rect(box.TranslatePoint(new Point(0, 0), flow), box.RenderSize);

                Assert.That(bounds.Right, Is.LessThanOrEqualTo(flow.ViewportWidth + 0.5), $"{view.Name}: a box crosses the flow's edge");

                foreach (var button in Descendants<Button>(box).Where(button => button.ActualWidth > 0))
                {
                    var edge = button.TranslatePoint(new Point(button.ActualWidth, 0), box).X;

                    Assert.That(edge, Is.LessThanOrEqualTo(box.ActualWidth + 0.5), $"{view.Name}: a button is cut off by its box");
                    Assert.That(button.DesiredSize.Width - button.Margin.Left - button.Margin.Right,
                        Is.LessThanOrEqualTo(button.ActualWidth + 0.5), $"{view.Name}: a button is clipped");
                }
            }

            foreach (var text in Descendants<Button>(control).SelectMany(Descendants<TextBlock>)
                         .Concat(Descendants<Grid>(control).Where(grid => grid.Name == "Chip").SelectMany(Descendants<TextBlock>)))
            {
                Assert.That(Fits(text), Is.True, $"{view.Name}: '{text.Text}' is cut off at {text.ActualWidth:0.#} px");
            }
        }
    }

    [Test]
    public void AnOpenBox_InANarrowFlow_TakesTheWidthLeft_AndItsStagesTrimTheirWordsWithATooltip()
    {
        var view = _views.BuildYourMarket();
        view.ExpandAllCommand.Execute(null);
        var control = LaidOut(view, ViewWidthIn(720, false), 900);
        var deepest = BoxOf(control, RedirectAnchoring);
        var stages = AllNamed<Button>(deepest, "Stage").ToList();

        Assert.That(deepest.ActualWidth, Is.LessThan(PieceNodeViewModel.OpenWidth));
        Assert.That(stages.SelectMany(stage => Descendants<TextBlock>(stage)).Select(text => text.TextTrimming),
            Has.All.EqualTo(TextTrimming.CharacterEllipsis));
        Assert.That(stages.Select(stage => stage.ToolTip), Has.All.Not.Null);
    }

    // A trimmed line ends in an ellipsis and has a tooltip; a wrapping line fits when its longest word does; any other
    // line when the whole of it does.
    private static bool Fits(TextBlock text)
    {
        if (text.Text.Length == 0 || text.Visibility != Visibility.Visible || !text.IsVisible && text.ActualWidth == 0
            || text.TextTrimming != TextTrimming.None)
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
