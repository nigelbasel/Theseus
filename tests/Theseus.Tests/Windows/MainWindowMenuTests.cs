using System.Text.RegularExpressions;
using System.Xml.Linq;
using Theseus.App.ViewModels;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// The main window's menus and toolbar as written (baseline WB6): every item has an access key, unique in its menu with
/// removed and disabled items counted, by W26's map (B5, B11, C9); nothing in the menu bar or toolbar hides when it does
/// not apply (W31, C8); every icon-only button is named (E9, G167); labels never change with the state (W30, C10).
/// </summary>
public partial class MainWindowMenuTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Lazy<XDocument> Window = new(() => XDocument.Load(LooseXaml.SourceFile("MainWindow.xaml")));

    private static XElement MenuBar => Window.Value.Descendants(Presentation + "Menu").Single();

    private static XElement CardMenu => Window.Value.Descendants(Presentation + "ContextMenu")
        .Single(menu => (string?)menu.Attribute(Xaml + "Key") == "CardMenu");

    private static XElement TopLevel(string header) =>
        MenuBar.Elements(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == header);

    private static IEnumerable<XElement> Items(XElement menu) => menu.Elements(Presentation + "MenuItem");

    // The label as written: the Header attribute, or the AccessText inside a header element.
    private static string? Label(XElement item) =>
        (string?)item.Attribute("Header")
        ?? item.Element(Presentation + "MenuItem.Header")?.Descendants(Presentation + "AccessText").Select(text => (string?)text.Attribute("Text")).FirstOrDefault();

    [GeneratedRegex("(?<!_)_([^_])")]
    private static partial Regex Marker();

    private static char? Key(XElement item) =>
        Label(item) is { } label && !label.StartsWith('{') && Marker().Match(label) is { Success: true } found
            ? char.ToUpperInvariant(found.Groups[1].Value[0])
            : null;

    private static IEnumerable<XElement> MenusWithItems()
    {
        yield return CardMenu;

        foreach (XElement menu in MenuBar.Descendants(Presentation + "MenuItem").Where(item => Items(item).Any()))
        {
            yield return menu;
        }
    }

    [Test]
    public void EveryMenuItem_HasAnAccessKey_UniqueInItsMenu_RemovedItemsIncluded()
    {
        foreach (XElement menu in MenusWithItems().Append(MenuBar))
        {
            List<XElement> items = [.. Items(menu)];
            List<char?> keys = [.. items.Select(Key)];
            string name = Label(menu) ?? (string?)menu.Attribute(Xaml + "Key") ?? "the menu bar";

            Assert.That(keys, Has.None.Null, $"{name}: every item has a key ({string.Join(", ", items.Where(item => Key(item) is null).Select(Label))})");
            Assert.That(keys, Is.Unique, $"{name}: {string.Join(" ", keys)}");
        }
    }

    [Test]
    public void TheViewMenu_FollowsTheW26Map() =>
        Assert.That(string.Concat(Items(TopLevel("_View")).Select(Key)), Is.EqualTo("SLCGAHPWJOIEKFNDUMVTBR"),
            "W26's map, with J for Show just active cards (2026-11-24) and I for Project view, every letter of Projects being taken (2026-11-26)");

    [Test]
    public void TheGroupBySubmenu_FollowsTheW26Map()
    {
        XElement groupBy = Items(TopLevel("_View")).Single(item => Label(item) == "_Group by");

        Assert.That(string.Concat(Items(groupBy).Select(Key)), Is.EqualTo("TSAFCNRB"));
    }

    [Test]
    public void TheWindowMenu_HoldsArrangeAndBringProjectWindowForward() =>
        Assert.That(Items(TopLevel("Win_dow")).Select(Label), Is.EqualTo(new[] { "_Arrange windows…", "_Bring project window forward" }));

    [Test]
    public void NoKey_UsesIOrO_ButOpenProjectAndProjectView_TheRecordedDeviations()
    {
        IEnumerable<XElement> all = MenusWithItems().SelectMany(Items);

        // Project view's every other letter is taken in View (board-small-changes item 3, 2026-11-26).
        Assert.That(all.Where(item => Key(item) is 'I' or 'O').Select(Label), Is.EqualTo(new[] { "_Open project", "Project v_iew" }));
    }

    [TestCase("O_ptions")]
    [TestCase("_About Theseus")]
    [TestCase("_Day report")]
    public void ACommandThatAsksNothingMore_HasNoEllipsis(string label) =>
        Assert.That(MenuBar.Descendants(Presentation + "MenuItem").Select(Label), Does.Contain(label));

    [Test]
    public void NothingInTheMenuBar_HidesByACondition_AndTheToolbarOnlyByWhatIsShown()
    {
        XElement toolBar = Window.Value.Descendants(Presentation + "ToolBar").Single();
        IEnumerable<XElement> tools = toolBar.Elements().Where(element => element.Name.LocalName is "Button" or "ToggleButton");

        Assert.That(MenuBar.Descendants(Presentation + "MenuItem")
            .Where(element => ((string?)element.Attribute("Visibility"))?.Contains("{Binding", StringComparison.Ordinal) == true)
            .Select(Label), Is.Empty, "the menu bar never changes shape");
        // The toolbar shows the commands of what is shown, the board's or the project's (project-view-as-tabs AC 6, 7; Nigel,
        // 2026-11-26); within that, a command that cannot run is greyed with its reason.
        Assert.That(tools.Select(element => (string?)element.Attribute("Visibility")).Where(visibility => visibility?.Contains("{Binding", StringComparison.Ordinal) == true),
            Is.All.EqualTo("{Binding IsBoardShown, Converter={StaticResource BoolToVisibility}}")
                .Or.EqualTo("{Binding IsProjectViewShown, Converter={StaticResource BoolToVisibility}}"));
    }

    [Test]
    public void EveryIconOnlyButton_InTheToolbarAndSearchBar_IsNamed()
    {
        IEnumerable<XElement> scope = Window.Value.Descendants(Presentation + "ToolBar")
            .Concat(Window.Value.Descendants().Where(element => (string?)element.Attribute(Xaml + "Name") is "SearchBar" or "BoardShowsChoice"));
        IEnumerable<XElement> glyphButtons = scope.Descendants()
            .Where(element => element.Name.LocalName is "Button" or "ToggleButton" or "RadioButton")
            .Where(element => element.Descendants().Concat([element]).Any(inner =>
                ((string?)inner.Attribute("Content") ?? (string?)inner.Attribute("Text"))?.Any(c => c is >= '' and <= '') == true));

        Assert.That(glyphButtons, Is.Not.Empty);
        Assert.That(glyphButtons.Where(button => string.IsNullOrWhiteSpace((string?)button.Attribute("AutomationProperties.Name"))), Is.Empty);
    }

    [Test]
    public void NoGlyph_UsesSegoeMdl2Assets_InTheMenusToolbarOrSearchBar()
    {
        IEnumerable<XElement> scope = new[] { MenuBar, CardMenu }.Concat(Window.Value.Descendants(Presentation + "ToolBarTray"));

        Assert.That(scope.DescendantsAndSelf().Attributes().Where(attribute => attribute.Value.Contains("Segoe MDL2", StringComparison.Ordinal)), Is.Empty);
    }

    [Test]
    public void TheCardMenusLabels_DoNotChangeWithTheState()
    {
        List<string?> headers = [.. CardMenu.Descendants(Presentation + "MenuItem").Select(item => (string?)item.Attribute("Header"))];

        Assert.That(headers.Where(header => header?.StartsWith("{Binding", StringComparison.Ordinal) == true), Is.Empty);
        Assert.That(headers, Is.SupersetOf(new[] { "_Pause", "_Unpause", "_Block", "Unbloc_k", "Re_name handover to match title" }));
    }

    [Test]
    public void TheArchiveCommand_CannotRun_WhileTheArchiveIsShowing()
    {
        Assert.That(SelectionReasons.Archive(archiveShown: true, ticked: 2, quiet: 2), Is.Not.Null);
        Assert.That(SelectionReasons.Archive(archiveShown: false, ticked: 2, quiet: 2), Is.Null);
        Assert.That(SelectionReasons.Restore(archiveShown: true, ticked: 2), Is.Null, "Restore is its pair while the archive shows");
        Assert.That(SelectionReasons.Restore(archiveShown: false, ticked: 2), Is.Not.Null);
    }

    [Test]
    public void ADisabledSelectionCommand_SaysWhy()
    {
        Assert.That(SelectionReasons.Start(ticked: 0, startable: 0), Is.EqualTo(SelectionReasons.NothingTicked));
        Assert.That(SelectionReasons.Start(ticked: 2, startable: 0), Does.Contain("already running"));
        Assert.That(SelectionReasons.Stop(ticked: 1, running: 0), Does.Contain("No ticked card has a session running"));
        Assert.That(SelectionReasons.Archive(archiveShown: false, ticked: 1, quiet: 0), Does.Contain("Stop it first"));
        Assert.That(SelectionReasons.Start(ticked: 1, startable: 1), Is.Null);
    }

    [Test]
    public void AReasonedCommand_FollowsItsReason()
    {
        string? why = "Not yet.";
        ReasonedCommand command = new(_ => { }, _ => why);

        Assert.That(command.CanExecute(null), Is.False);
        Assert.That(command.WhyNot, Is.EqualTo("Not yet."));

        why = null;

        Assert.That(command.CanExecute(null), Is.True);
        Assert.That(command.WhyNot, Is.Null);
    }
}
