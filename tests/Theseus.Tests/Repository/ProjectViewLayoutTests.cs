using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Automation;
using Path = System.Windows.Shapes.Path;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;
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

    private static ProjectViewModel WithRunsOpen(ProjectViewModel view)
    {
        foreach (var run in view.StateGroups.Where(group => !group.IsOpen).ToList())
        {
            run.ToggleCommand.Execute(null);
        }

        return view;
    }

    private static Border BoxOf(ProjectView control, string piece) =>
        AllNamed<Border>(control, "Box").Single(box => box.DataContext is PieceNodeViewModel node && node.Name == piece);

    [Test]
    public void TheTitleBar_StartsWithBackToTheBoard_ThenATabPerProject()
    {
        WriteBuildYourMarket(_folder);
        var control = LaidOut(_views.Theseus());
        var back = (Button)control.FindName("BackButton");
        var tabs = Descendants<Button>(control).Where(button => button.DataContext is ProjectTabViewModel).ToList();

        Assert.That(Descendants<Button>(control).Take(3).Select(button => button.Name), Is.EqualTo(new[] { "GoBackButton", "GoForwardButton", "BackButton" }),
            "Back, Forward, then the board, then the tabs (1.1f I46)");
        Assert.That((back.Content, back.ToolTip), Is.EqualTo(("⊞ Board", "Leave the project for the board")));
        Assert.That(tabs.Select(tab => ((ProjectTabViewModel)tab.DataContext).Title), Is.EquivalentTo(new[]
        {
            "Theseus bootstrap", "BuildYourMarket MCP server",
        }));
        Assert.That(tabs.Select(tab => Named<Rectangle>(tab, "Underline").Visibility == Visibility.Visible),
            Is.EquivalentTo(new[] { true, false }), "only the open tab is underlined");
    }

    [Test]
    public void TheStackedCardMark_IsThreeOffsetOutlines_20By16()
    {
        var control = LaidOut(_views.Theseus());
        var mark = Descendants<Canvas>(Named<Border>(control, "Where")).First();
        var outlines = mark.Children.OfType<Rectangle>().ToList();

        Assert.That((mark.Width, mark.Height), Is.EqualTo((20.0, 16.0)), "on the 4 epx grid (baseline W36, D9)");
        Assert.That(outlines.Select(each => (Canvas.GetLeft(each), Canvas.GetTop(each), each.Width, each.Height)), Is.EqualTo(new[]
        {
            (5.0, 1.0, 14.0, 10.0), (3.0, 3.0, 14.0, 10.0), (1.0, 5.0, 14.0, 10.0),
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
        Assert.That(((TextBlock)control.FindName("WaitsHeading")).Text, Is.EqualTo($"◆ Waits for you ({view.Waits.Count})"));
        Assert.That(Descendants<Button>(band).Where(button => button.Visibility == Visibility.Visible).Select(button => button.Content),
            Has.Exactly(view.Waits.Count(wait => wait.HasButton)).Items);
        Assert.That(((Border)LaidOut(_views.BuildYourMarket()).FindName("WaitingBand")).Visibility, Is.EqualTo(Visibility.Visible),
            "always in place, even when nothing waits (G36)");
    }

    [Test]
    public void TheFlow_IsAScrollViewer_HoldingTheRoots()
    {
        var control = LaidOut(_views.Theseus());
        var flow = (ScrollViewer)control.FindName("Flow");
        var content = (Grid)control.FindName("FlowContent");
        var frame = (Border)control.FindName("FlowFrame");

        Assert.That((flow.HorizontalScrollBarVisibility, flow.VerticalScrollBarVisibility),
            Is.EqualTo((ScrollBarVisibility.Auto, ScrollBarVisibility.Auto)));
        Assert.That(content.Children[0], Is.SameAs(control.FindName("RootList")));
        Assert.That(frame.Padding, Is.EqualTo(new Thickness(20)));
        Assert.That(frame.MaxWidth, Is.EqualTo(flow.ViewportWidth), "the flow is never wider than what shows of it");
    }

    [Test]
    public void AFoldedBox_Is290Wide_Rounded_WithItsKindsTitleBar_ItsTitle_AndAStripOfSevenChipsAndTheGate()
    {
        var control = LaidOut(WithRunsOpen(_views.Theseus()));
        var box = BoxOf(control, "stop-timing");
        var chips = AllNamed<Grid>(box, "Chip").ToList();

        Assert.That((box.ActualWidth, box.CornerRadius.TopLeft), Is.EqualTo((290.0, 8.0)), "a rounded action node (D17)");
        Assert.That(((Button)Named<Button>(box, "Chevron")).Content, Is.EqualTo("▸"));
        Assert.That(Descendants<TextBlock>(Named<Border>(box, "KindWord")).Single().Text, Is.EqualTo("⚒ Build"));
        Assert.That(Named<TextBlock>(box, "PieceName").Text, Is.EqualTo(((PieceNodeViewModel)box.DataContext).Title));
        Assert.That(Named<TextBlock>(box, "WhoActs").Text, Is.EqualTo("No action needed."));
        Assert.That(Descendants<TextBlock>(Named<Button>(box, "Serves")).Single().Text, Is.EqualTo("serves Objective 0.3"));
        Assert.That(AllNamed<TextBlock>(box, "Gate").Single().Text, Is.EqualTo("◆"), "done before the gates were kept");
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
            "Open spec", "Accept spec…", "Independent check…", "⧉ Focus git-22", "■ Stop", "Open records",
        }));
        Assert.That(Named<ItemsControl>(box, "Strip").Visibility, Is.EqualTo(Visibility.Collapsed));
    }

    [Test]
    public void AnOpenStateHeading_ShowsItsBoxes_NotAlsoTheLineOfTheirNames()
    {
        // Walkthrough row 38: open, the run showed its boxes and the paragraph of their names below them.
        var view = _views.Theseus();
        var run = view.StateGroups.First(group => group.State == StageNodeState.Done);
        run.ToggleCommand.Execute(null);
        var control = LaidOut(view);
        var titles = AllNamed<TextBlock>(control, "StateTitles").Where(text => text.DataContext == run).ToList();

        Assert.That(titles, Is.Not.Empty);
        Assert.That(titles.Select(text => text.IsVisible || (text.Visibility == Visibility.Visible && text.ActualHeight > 0)), Has.All.False);
    }

    [Test]
    public void TheSelectorBar_HasOnlyTheShownViewAsATabStop_AndTheArrowsSelect()
    {
        // Checklist G124: Tab lands on the selected tab and leaves the bar; the arrows select (P2 review, should-fix 2).
        var view = _views.Theseus();
        var control = LaidOut(view);
        var flow = Named<RadioButton>(control, "GroupByFlow");
        var objectives = Named<RadioButton>(control, "GroupByObjectives");
        var shown = view.IsGroupedByFlow ? flow : objectives;
        var other = view.IsGroupedByFlow ? objectives : flow;

        Assert.Multiple(() =>
        {
            Assert.That((shown.IsTabStop, other.IsTabStop), Is.EqualTo((true, false)));
            Assert.That(Theseus.App.Controls.SelectorBar.GetSelectsOnFocus(other), Is.True);
            Assert.That(shown.ActualHeight, Is.GreaterThanOrEqualTo(32), "a 32 hit area (baseline W38)");
        });
    }

    [Test]
    public void SetOutToMeet_IsOneLineFolded_AndATableOpen_ThatARefreshKeepsOpen()
    {
        // Walkthrough row 37: "very busy, a freeform list that just flows"; an expandable table was asked for.
        var view = _views.BuildYourMarket();
        var round = view.Rounds.First(each => each.Objectives.Count > 1);
        var folded = LaidOut(view);

        Assert.That(round.MeetSummary, Does.Match(@"^\d+ objectives · \d+ of \d+ criteria met$"));
        Assert.That(AllNamed<ItemsControl>(folded, "RoundObjectives").Where(table => table.DataContext == round).Single().IsVisible
                    || AllNamed<ItemsControl>(folded, "RoundObjectives").Where(table => table.DataContext == round).Single().Visibility == Visibility.Visible,
            Is.False, "folded at first");

        view.HighlightObjective(round.Objectives[0].Objective.Number);

        Assert.That(round.HasHighlightedObjective, Is.True, "hovering a box still shows its objective, on the folded line (1.1f I45)");

        round.IsMeetOpen = true;
        var control = LaidOut(view);
        var bars = AllNamed<Border>(control, "ObjectiveChip")
            .Where(row => row.DataContext is RoundObjectiveViewModel chip && round.Objectives.Contains(chip))
            .Select(row => Descendants<Grid>(row).First(grid => Grid.GetColumn(grid) == 1))
            .ToList();

        Assert.That(bars, Has.Count.EqualTo(round.Objectives.Count));
        Assert.That(bars.Select(bar => Math.Round(bar.TranslatePoint(new Point(0, 0), control).X)).Distinct().Count(), Is.EqualTo(1),
            "the bars line up in one column, however long each title");
        Assert.That(_views.BuildYourMarket(previous: view).Rounds.First(each => each.Round.Number == round.Round.Number).IsMeetOpen, Is.True);
    }

    [Test]
    public void ThePhaseStrip_IsOneLine_HoweverManyPhases()
    {
        // Walkthrough row 17: the strip wrapped onto a second line at Phase 6.
        var control = LaidOut(_views.BuildYourMarket());
        var cells = AllNamed<Border>(control, "PhaseCell").ToList();

        Assert.That(cells, Has.Count.GreaterThan(1));
        Assert.That(cells.Select(cell => Math.Round(cell.TranslatePoint(new Point(0, 0), control).Y)).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public void AnOpenBoxsActions_AreIcons_OneHeight_EachNamedAndExplainedInItsTooltip()
    {
        // Walkthrough rows 24 and 32: long words in a row of buttons cluttered the box; the board's icons were wanted, with
        // the explanation in the tooltip (checklist G38, G40, G86, G92).
        var view = _views.Theseus();
        view.ExpandAllCommand.Execute(null);
        var buttons = Descendants<Button>(Named<ItemsControl>(BoxOf(LaidOut(view), "projects-and-method-ui"), "Actions")).ToList();

        Assert.Multiple(() =>
        {
            foreach (var button in buttons)
            {
                var action = (PieceActionViewModel)button.DataContext;

                Assert.That(button.Content, Is.EqualTo(action.Glyph).And.Not.Null, action.Label);
                Assert.That((string)button.ToolTip, Does.StartWith(ActionGlyph.Name(action.Label)), action.Label);
                Assert.That(System.Windows.Automation.AutomationProperties.GetName(button), Is.EqualTo(ActionGlyph.Name(action.Label)), "read without the marks");
            }

            Assert.That(buttons.Select(button => button.ActualHeight).Distinct(), Is.EqualTo(new[] { 32.0 }), "one height in the row");
        });
    }

    [Test]
    public void ATooltipUnderThePointer_ShowsWithinTheViewsShortDelay()
    {
        var control = LaidOut(_views.Theseus());
        var tipped = new TextBlock { ToolTip = "tip" };
        ((Panel)control.FindName("FlowContent")).Children.Add(tipped);

        control.QuickToolTips(tipped);

        Assert.That(ToolTipService.GetInitialShowDelay(tipped), Is.EqualTo(ProjectView.ToolTipDelay), "review of PR 5, finding 3");
    }

    [Test]
    public void TabReachesTheChevron_ThenTheName_ThenTheStages_ThenTheActions_InReadingOrder()
    {
        var view = _views.Theseus();
        view.ExpandAllCommand.Execute(null);
        var box = BoxOf(LaidOut(view), "projects-and-method-ui");

        var stops = Descendants<FrameworkElement>(box).Where(element => element.Focusable && KeyboardNavigation.GetIsTabStop(element))
            .Select(element => element.Name).ToList();

        Assert.That(stops, Is.EqualTo(new[] { "Chevron", "PieceHeader" }.Concat(Enumerable.Repeat("Stage", 7)).Append("Serves")
            .Concat(Enumerable.Repeat("Action", 6))));
    }

    [Test]
    public void TheToolbar_IsOneSize_ItsThreeFoldButtonsSmallIcons32High()
    {
        // Nigel, 2026-11-23: Collapse all, Expand all and Open what needs attention were "large buttons much larger than
        // everything else in that row". Their names, keys and tooltips stay; only their size and face change.
        var control = LaidOut(_views.Theseus());

        foreach (var (name, spoken) in new[] { ("CollapseAll", "Collapse all"), ("ExpandAll", "Expand all"), ("OpenWhatNeedsAttention", "Open what needs attention") })
        {
            var button = Named<Button>(control, name);

            Assert.Multiple(() =>
            {
                Assert.That(button.ActualHeight, Is.EqualTo(32).Within(0.5), name);
                Assert.That(button.ActualWidth, Is.LessThanOrEqualTo(button.ActualHeight + 20), $"{name} is a small icon, not a word");
                Assert.That(AutomationProperties.GetName(button), Is.EqualTo(spoken), "UI Automation reads the same name as before");
                Assert.That(button.ToolTip as string, Does.StartWith(spoken), "its hover says what it does");
                Assert.That(KeyboardNavigation.GetIsTabStop(button) && button.Focusable && button.IsEnabled, Is.True, "still reached with the keyboard");
            });
        }
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

        Assert.That(((TextBlock)control.FindName("PropertiesHeading")).Text, Is.EqualTo("Checks shown on cards"));
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
    public void AnOpenBox_InANarrowFlow_TakesTheWidthLeft_AndItsStagesWrapRatherThanCutAWord()
    {
        var view = _views.BuildYourMarket();
        view.ExpandAllCommand.Execute(null);
        var control = LaidOut(view, ViewWidthIn(720, false), 900);
        var deepest = BoxOf(control, RedirectAnchoring);
        var stages = AllNamed<Button>(deepest, "Stage").ToList();

        Assert.That(deepest.ActualWidth, Is.LessThan(PieceNodeViewModel.OpenWidth));
        Assert.That(stages.SelectMany(stage => Descendants<TextBlock>(stage)).Select(text => (text.TextTrimming, text.TextWrapping)),
            Has.All.EqualTo((TextTrimming.None, TextWrapping.Wrap)), "a stage's word is never cut (review of PR 3, finding 2)");
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
