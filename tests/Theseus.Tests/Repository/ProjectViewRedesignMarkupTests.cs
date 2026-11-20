using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// The rebuilt project view's markup (1.1d AC 16, 22, 25, 29, 35 and the state colours): back to the board by ← Board,
/// Alt+Left and the View menu, never by Esc; the legend; the stage menu; the splitter; every resource the view needs
/// defined in the view; and no word a user should not see.
/// </summary>
[TestFixture]
public partial class ProjectViewRedesignMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    private static XDocument View() => Load("Views", "ProjectView.xaml");

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == name);

    [Test]
    public void BackToTheBoard_IsTheButton_AltLeft_AndTheViewMenu_AndEscIsNotBack()
    {
        var window = Load("MainWindow.xaml");
        var bindings = window.Descendants(Presentation + "KeyBinding").ToList();
        var back = Named(View(), "BackButton");

        Assert.That((string?)back.Attribute("Command"), Is.EqualTo("{Binding BackCommand}"));
        Assert.That((string?)bindings.Single(binding => (string?)binding.Attribute("Gesture") == "Alt+Left").Attribute("Command"),
            Is.EqualTo("{Binding CloseProjectCommand}"));
        Assert.That(window.Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == "Back to the boar_d")
            .Attribute("InputGestureText")?.Value, Is.EqualTo("Alt+Left"));
        Assert.That((string?)View().Descendants(Presentation + "KeyBinding").Single(binding => (string?)binding.Attribute("Key") == "Escape")
            .Attribute("Command"), Is.EqualTo("{Binding ClearSelectionCommand}"), "Esc in the view clears the selection");
        Assert.That((string?)bindings.Single(binding => (string?)binding.Attribute("Gesture") == "Escape").Attribute("Command"),
            Is.EqualTo("{Binding ClearSelectionCommand}"), "the window's Esc, which ProjectViewHost routes to the view first");
    }

    [Test]
    public void TheView_TakesFocus_WhenClickedAnywhere()
    {
        var root = View().Root!;

        Assert.That((string?)root.Attribute("Focusable"), Is.EqualTo("True"));
        Assert.That((string?)root.Attribute("PreviewMouseDown"), Is.EqualTo("OnPreviewMouseDown"));
    }

    [Test]
    public void TheBreadcrumbAndTheTrunkFooter_AreGone()
    {
        var values = View().Descendants().Attributes().Select(attribute => attribute.Value).ToList();

        Assert.That(values, Has.None.EqualTo("BoardCrumb").And.None.Contains("MainPullRequestText"));
    }

    [Test]
    public void TheLegend_EndsWithTheTwoLinesAndTheReportBackMark()
    {
        var legend = Named(View(), "Legend");
        var words = legend.Descendants(Presentation + "TextBlock").Select(text => (string?)text.Attribute("Text"))
            .Where(text => text is not null && !text.StartsWith('{')).ToList();

        Assert.That(words, Is.EqualTo(new[] { "hands work to", "then worked on", "↩ reported back" }));
        Assert.That((string?)Named(View(), "HandsOverSample").Attribute("X2"), Is.EqualTo("26"));
        Assert.That((string?)Named(View(), "ThenWorkedOnSample").Attribute("StrokeDashArray"), Is.EqualTo("1 2"));
    }

    [Test]
    public void AStageButton_HasItsMenu_PropertiesInBold()
    {
        var stage = Named(View(), "Stage");
        var menu = stage.Descendants(Presentation + "ContextMenu").Single();
        var style = View().Descendants(Presentation + "Style").Single(each => (string?)each.Attribute(Xaml + "Key") == "StageMenuItem");

        Assert.That((string?)menu.Attribute("ItemsSource"), Is.EqualTo("{Binding MenuItems}"));
        Assert.That(style.Descendants(Presentation + "DataTrigger").Single().Attribute("Binding")!.Value, Is.EqualTo("{Binding IsDefault}"));
    }

    [Test]
    public void TheSplitter_IsSixPixels_WithItsCursorTooltipAndHandlers()
    {
        var splitter = Named(View(), "Splitter");

        Assert.That((string?)splitter.Attribute("Width"), Is.EqualTo("6"));
        Assert.That((string?)splitter.Attribute("Cursor"), Is.EqualTo("SizeWE"));
        Assert.That((string?)splitter.Attribute("ToolTip"), Is.EqualTo("Drag to resize; double-click to reset"));
        Assert.That((string?)splitter.Attribute("DragCompleted"), Is.EqualTo("OnSplitterDragCompleted"));
        Assert.That((string?)splitter.Attribute("MouseDoubleClick"), Is.EqualTo("OnSplitterDoubleClick"));
        Assert.That((string?)Named(View(), "PaneColumn").Attribute("MinWidth"), Is.EqualTo("260"));
    }

    [Test]
    public void EveryStaticResource_TheViewUses_IsDefinedInTheView()
    {
        var view = View();
        var keys = view.Descendants().Select(element => (string?)element.Attribute(Xaml + "Key")).OfType<string>().ToHashSet();
        var used = view.Descendants().Attributes()
            .SelectMany(attribute => StaticResource().Matches(attribute.Value).Select(match => match.Groups["key"].Value))
            .Distinct()
            .ToList();

        Assert.That(used, Is.Not.Empty);
        Assert.That(used.Where(key => !keys.Contains(key)), Is.Empty, "the tests load the view without App.xaml's resources");
    }

    [Test]
    public void NoTextTheViewShows_SaysSlugOrWorkItem()
    {
        var shown = View().Descendants().Attributes()
            .Where(attribute => attribute.Name.LocalName is "Text" or "Content" or "ToolTip" or "Header")
            .Select(attribute => attribute.Value);

        Assert.That(shown, Has.None.Match("(?i)slug|work item"));
    }

    [Test]
    public void TheStateColours_HaveALightAndADarkValueForEveryState()
    {
        var light = Colours("StateColours.Light.xaml");
        var dark = Colours("StateColours.Dark.xaml");

        Assert.That(dark.Keys, Is.EquivalentTo(light.Keys));
        Assert.That(new[] { "Done", "Waiting", "Busy", "Stuck", "Holdouts", "ToDo" }.Select(state => light[$"State{state}Brush"]),
            Is.EqualTo(new[] { "#2e8b57", "#b7791f", "#2563c9", "#c0392b", "#8a5cc7", "#8a9490" }));
        Assert.That(new[] { "Done", "Waiting", "Busy", "Stuck", "Holdouts", "ToDo" }.Select(state => dark[$"State{state}SoftBrush"]),
            Is.EqualTo(new[] { "#183026", "#33281a", "#1a2640", "#3a1f1c", "#2a2238", "#222a27" }));
        Assert.That((light["ConnectorBrush"], dark["ConnectorBrush"]), Is.EqualTo(("#6b7672", "#8d9994")));
        Assert.That(Load("App.xaml").Descendants(Presentation + "ResourceDictionary").Select(each => (string?)each.Attribute("Source")),
            Does.Contain("Themes/StateColours.Light.xaml"));
    }

    private static Dictionary<string, string> Colours(string file) =>
        Load("Themes", file).Descendants(Presentation + "SolidColorBrush")
            .ToDictionary(brush => (string)brush.Attribute(Xaml + "Key")!, brush => (string)brush.Attribute("Color")!);

    [GeneratedRegex(@"\{StaticResource (?<key>[A-Za-z]+)\}")]
    private static partial Regex StaticResource();

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
