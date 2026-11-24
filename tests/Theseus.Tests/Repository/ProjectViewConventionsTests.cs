using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Theseus.App.ViewModels;
using Theseus.App.Views;
using Theseus.Tests.Projects;
using Theseus.Tests.ViewModels;
using Theseus.Tests.Windows;

namespace Theseus.Tests.Repository;

/// <summary>
/// The project view on the Windows conventions (baseline WB8; checklist G64 to G72, G82, G86 to G92, G112, G124, G155,
/// G161, G166 to G172): its tab strip is a selector, its keys and splitter work from the keyboard, its glyphs are the
/// icon font's, its links are links and its actions buttons, and UI Automation reads what is on the screen. Laid out
/// as WPF lays it out, without the application's resources; no test moves keyboard focus.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectViewConventionsTests
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

    [Test]
    public void TheTabStrip_IsASelector_WithOneTabStop_TheArrowKeysInside_AndTheOpenProjectSelected()
    {
        ProjectViewFixtures.WriteBuildYourMarket(_folder);
        ProjectView control = LaidOut(_views.Theseus());
        Selector tabs = (Selector)control.FindName("Tabs");

        Assert.That(KeyboardNavigation.GetTabNavigation(tabs), Is.EqualTo(KeyboardNavigationMode.Once));
        Assert.That(KeyboardNavigation.GetDirectionalNavigation(tabs), Is.EqualTo(KeyboardNavigationMode.Contained));
        Assert.That(((ProjectTabViewModel)tabs.SelectedItem).IsOpen, Is.True);
        Assert.That(AutomationProperties.GetName(tabs), Is.EqualTo("Project tabs"));
    }

    [Test]
    public void TheTabStripsSelection_FollowsTheOpenProject_WhenAnotherProjectOpens()
    {
        ProjectViewFixtures.WriteTheseus(_folder);
        ProjectView control = LaidOut(_views.Theseus());
        Selector tabs = (Selector)control.FindName("Tabs");

        control.DataContext = _views.BuildYourMarket();
        control.UpdateLayout();

        Assert.That(((ProjectTabViewModel)tabs.SelectedItem).Name, Is.EqualTo(ProjectViewFixtures.BuildYourMarket),
            "a tab key opens another project, and the strip selects its tab (WB6's tab commands rebuild the view)");
    }

    [Test]
    public void SelectingATab_OpensItsProject()
    {
        ProjectViewFixtures.WriteBuildYourMarket(_folder);
        RecordingCommand open = new();
        ProjectView control = LaidOut(_views.Theseus(tabCommands: _ => open));
        Selector tabs = (Selector)control.FindName("Tabs");

        tabs.SelectedItem = tabs.Items.Cast<ProjectTabViewModel>().Single(tab => !tab.IsOpen);

        Assert.That(open.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public void APeerWalk_FindsNoTypeNameOrRecordDump()
    {
        ProjectViewModel view = _views.BuildYourMarket();
        OpenEverything(view);
        ProjectView control = LaidOut(view);
        List<string> names = [];

        Walk(UIElementAutomationPeer.CreatePeerForElement(control), names);

        Assert.That(names, Is.Not.Empty);
        Assert.That(names.Where(name => name.StartsWith("Theseus.", StringComparison.Ordinal) || name.Contains(" { ", StringComparison.Ordinal)),
            Is.Empty, "W50, E11: a list item says what it shows");
    }

    [Test]
    public void EveryStripChip_ShowsItsStateMark_NotColourAlone()
    {
        ProjectView control = LaidOut(_views.BuildYourMarket());
        List<FrameworkElement> chips = [.. Descendants<FrameworkElement>(control).Where(element => element.Name == "Chip")];

        Assert.That(chips, Is.Not.Empty);
        Assert.That(chips.Select(chip => (Mark: ((StageNodeViewModel)chip.DataContext).Mark,
                Text: string.Concat(Descendants<TextBlock>(chip).Select(text => text.Text)))).Where(each => !each.Text.Contains(each.Mark, StringComparison.Ordinal)),
            Is.Empty, "E5: a state mark in each strip chip");
    }

    [Test]
    public void CtrlZ_UndoesTheClearUp_WhileItCan()
    {
        List<string> undone = [];
        ProjectViewModel view = _views.BuildYourMarket(clearUpNotice: "Removed 2 verifier cards.",
            actions: _views.Actions with { UndoClearVerifiers = name => undone.Add(name) });
        ProjectView control = LaidOut(view);
        KeyBinding undo = control.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Z && binding.Modifiers == ModifierKeys.Control);

        Assert.That(undo.Command.CanExecute(null), Is.True);
        undo.Command.Execute(null);

        Assert.That(undone, Is.EqualTo(new[] { view.Name }));
    }

    [Test]
    public void TheSplitter_IsFocusable_SoTheArrowKeysResizeThePane()
    {
        ProjectView control = LaidOut(_views.Theseus());
        GridSplitter splitter = (GridSplitter)control.FindName("Splitter");

        Assert.That(splitter.Focusable, Is.True);
        Assert.That(splitter.Width, Is.InRange(8.0, 12.0), "W38: an 8 to 12 epx hit area");
    }

    [Test]
    public void TheToolbarIcons_ShareOneHeightOfAtLeast32_WithGroupBy_AndAreNamedForWhatTheyDo()
    {
        ProjectView control = LaidOut(_views.Theseus());
        string[] icons = ["FocusLeadIcon", "FoldAll", "UnfoldAll", "OpenWhatNeedsAttention"];
        double groupBy = ((FrameworkElement)control.FindName("GroupByFlow")).ActualHeight;

        Assert.That(groupBy, Is.GreaterThanOrEqualTo(32));
        Assert.That(icons.Select(name => ((FrameworkElement)control.FindName(name))?.ActualHeight), Is.All.EqualTo(groupBy));
        Assert.That(Descendants<Button>((DependencyObject)control.FindName("Counts")).Where(chip => chip.Name == "ChipButton").Select(chip => chip.ActualHeight),
            Is.Not.Empty.And.All.EqualTo(groupBy), "the count chips in the toolbar's row too (W56)");
        Assert.That(icons.Skip(1).Select(name => AutomationProperties.GetName((DependencyObject)control.FindName(name))),
            Is.EqualTo(new[] { "Fold all", "Unfold all", "Open what needs attention" }));
        Assert.That(((Button)control.FindName("FoldAll")).Content, Is.EqualTo(""), "W25: the board's fold glyph");
        Assert.That(((Button)control.FindName("UnfoldAll")).Content, Is.EqualTo(""));
        Assert.That(((Button)control.FindName("OpenWhatNeedsAttention")).ToolTip, Is.EqualTo("Open what needs attention"),
            "C21: the label; the explanation is in HelpText");
    }

    [Test]
    public void TheCommonButtons_HaveAccessKeys_UniqueInTheView_AndNoneTheSearchBarUses()
    {
        ProjectView control = LaidOut(_views.BuildYourMarket(clearUpNotice: "Removed 2 verifier cards.",
            actions: _views.Actions with { UndoClearVerifiers = _ => { }, DismissClearUp = _ => { } }));
        Dictionary<string, string> expected = new()
        {
            ["GoBackButton"] = "Bac_k", ["GoForwardButton"] = "_Forward", ["BackButton"] = "_Board",
            ["UndoClearUp"] = "_Undo", ["DismissClearUp"] = "_Dismiss",
        };

        foreach ((string name, string label) in expected)
        {
            Button button = (Button)control.FindName(name);
            Assert.That(Descendants<AccessText>(button).Select(text => text.Text), Does.Contain(label), name);
        }

        List<char> keys = [.. Descendants<AccessText>(control).Where(text => text.AccessKey != '\0').Select(text => char.ToUpperInvariant(text.AccessKey))];
        Assert.That(keys, Is.Unique);
        Assert.That(keys, Has.None.EqualTo('R').And.None.EqualTo('C'), "the board's search bar has Hide the _rest and _Clear");
    }

    [Test]
    public void TheClearUpButtons_AreInReadingOrder_UndoBeforeDismiss()
    {
        ProjectView control = LaidOut(_views.BuildYourMarket(clearUpNotice: "Removed 2 verifier cards.",
            actions: _views.Actions with { UndoClearVerifiers = _ => { }, DismissClearUp = _ => { } }));
        List<string> order = [.. Descendants<Button>(control).Select(button => button.Name).Where(name => name is "UndoClearUp" or "DismissClearUp")];
        Button dismiss = (Button)control.FindName("DismissClearUp");

        Assert.That(order, Is.EqualTo(new[] { "UndoClearUp", "DismissClearUp" }), "B2: Tab reaches Undo first");
        Assert.That(AutomationProperties.GetName(dismiss), Is.EqualTo("Dismiss"), "E15: the name says what is on the screen");
        Assert.That(AutomationProperties.GetHelpText(dismiss), Does.Contain("can no longer be undone"));
    }

    [Test]
    public void EveryFoldChevron_IsTheIconFontsChevron_AndNamed()
    {
        ProjectViewModel view = _views.BuildYourMarket();
        view.IsGroupedByObjectives = true;
        ProjectView control = LaidOut(view);
        List<Button> chevrons = [.. Descendants<Button>(control).Where(button => button.Name is "ObjectiveChevron" or "PhaseChevron" or "Chevron")];

        Assert.That(chevrons, Is.Not.Empty);
        Assert.That(chevrons.Select(button => button.Content), Is.All.EqualTo("").Or.EqualTo(""), "W25: E70D open, E76C folded");
        Assert.That(chevrons.Select(button => AutomationProperties.GetName(button)), Has.None.Empty, "C6, E9");
    }

    [Test]
    public void RealLinks_AreLinkButtons_AndNoActionIsDrawnAsALink()
    {
        ProjectViewModel view = _views.BuildYourMarket();
        OpenEverything(view);
        ProjectView control = LaidOut(view);
        object linkStyle = control.FindResource("LinkButton");
        object linkTemplate = control.FindResource("LinkTemplate");
        List<Button> buttons = [.. Descendants<Button>(control)];

        Assert.That(buttons.Where(button => button.Name == "Serves"), Is.Not.Empty.And.All.Matches<Button>(button => ReferenceEquals(button.Style, linkStyle)),
            "WB1 D-10: the way to the objective a piece serves is a link, underlined at rest");
        Assert.That(buttons.Where(button => ReferenceEquals(button.Template, linkTemplate)).Select(button => button.Name), Is.Empty,
            "W15: a click surface that is not a link takes no link look");
    }

    [Test]
    public void TheKindBarsWords_TakeTheOnKindBrushes_SoTheyReadInEveryContrastTheme()
    {
        ProjectView control = new() { DataContext = _views.BuildYourMarket(), Width = 1400, Height = 900 };
        SolidColorBrush onKind = new(Colors.Lime);
        SolidColorBrush wash = new(Colors.Navy);
        control.Resources["OnKindTextBrush"] = onKind;
        control.Resources["KindWordWashBrush"] = wash;
        Lay(control);

        TextBlock name = Descendants<TextBlock>(control).First(text => text.Name == "PieceName");
        Border word = Descendants<Border>(control).First(border => border.Name == "KindWord");

        Assert.That(name.Foreground, Is.SameAs(onKind), "WB2 decision 10: literal White vanished under Desert");
        Assert.That(word.Background, Is.SameAs(wash));
        Assert.That(Descendants<TextBlock>(word).Single().Foreground, Is.SameAs(onKind));
    }

    [Test]
    public void TheObjectiveChip_OpensOnSpace_AsOnEnter()
    {
        ProjectViewModel view = _views.BuildYourMarket();

        foreach (RoundViewModel round in view.ShownRounds)
        {
            round.IsMeetOpen = true;
        }

        ProjectView control = LaidOut(view);
        Border chip = Descendants<Border>(control).First(border => border.Name == "ObjectiveChip");

        Assert.That(chip.InputBindings.OfType<KeyBinding>().Select(binding => binding.Key), Is.SupersetOf(new[] { Key.Enter, Key.Space }), "B4, W44");
    }

    [Test]
    public void TheWording_FollowsW17()
    {
        ProjectView control = LaidOut(_views.BuildYourMarket());

        Assert.That(((TextBlock)control.FindName("ProjectTag")).Text, Is.EqualTo("Project"), "A20: a tag, in sentence case");
        Assert.That(((TextBlock)control.FindName("ProjectTag")).FontSize, Is.EqualTo(12.0));
        Assert.That(((FrameworkElement)control.FindName("WaitsHeading")).GetValue(TextBlock.FontWeightProperty), Is.EqualTo(FontWeights.SemiBold), "A27");
    }

    private ProjectView LaidOut(ProjectViewModel view)
    {
        ProjectView control = new() { DataContext = view, Width = 1400, Height = 900 };
        Lay(control);

        return control;
    }

    private static void Lay(ProjectView control)
    {
        control.Measure(new Size(control.Width, control.Height));
        control.Arrange(new Rect(0, 0, control.Width, control.Height));
        control.UpdateLayout();
    }

    private static void OpenEverything(ProjectViewModel view)
    {
        view.ExpandAllCommand.Execute(null);
    }

    private static void Walk(AutomationPeer? peer, List<string> names)
    {
        if (peer is null)
        {
            return;
        }

        string name = peer.GetName();

        if (!string.IsNullOrEmpty(name))
        {
            names.Add(name);
        }

        foreach (AutomationPeer child in peer.GetChildren() ?? [])
        {
            Walk(child, names);
        }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
