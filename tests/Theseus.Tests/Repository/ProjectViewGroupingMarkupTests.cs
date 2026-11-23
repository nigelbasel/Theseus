using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// The markup of PR 4 (1.1d AC 45, 47, 49, 51, 53): Group by on the toolbar and on the View menu beside Rows by, one box
/// template for both groupings, the headings' menus, and the Properties overlay with its close button.
/// </summary>
[TestFixture]
public class ProjectViewGroupingMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    private static XDocument View() => Load("Views", "ProjectView.xaml");

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(Xaml + "Name") == name);

    [Test]
    public void TheTwoViews_AreASelectorBarOverThePage_OneSelected_OneTabStop()
    {
        var view = View();
        var buttons = Named(view, "GroupBy").Elements(Presentation + "RadioButton").ToList();
        var style = view.Descendants(Presentation + "Style").Single(each => (string?)each.Attribute(Xaml + "Key") == "GroupByButton");

        Assert.That(buttons.Select(button => ((string?)button.Attribute("Content"), (string?)button.Attribute("IsChecked"))), Is.EqualTo(new[]
        {
            ("_Rounds and work", "{Binding IsGroupedByFlow, Mode=TwoWay}"), ("_Objectives only", "{Binding IsGroupedByObjectives, Mode=TwoWay}"),
        }));
        // Walkthrough row 44: two views of the same data are tabs over the page (checklist G124, G125), not a toolbar's toggles.
        Assert.That(Named(view, "Toolbar").Descendants().Any(element => (string?)element.Attribute(Xaml + "Name") == "GroupBy"), Is.False);
        Assert.That((string?)Named(view, "GroupBy").Attribute("KeyboardNavigation.TabNavigation"), Is.EqualTo("Once"));
        Assert.That(style.Descendants(Presentation + "Rectangle").Single().Attribute(Xaml + "Name")!.Value, Is.EqualTo("Underline"));
        Assert.That(style.Elements(Presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "GroupName")
            .Attribute("Value")!.Value, Is.EqualTo("GroupBy"), "pressing one releases the other");
    }

    [Test]
    public void TheViewMenu_HasGroupProjectBy_BesideRowsBy_WhileAProjectIsOpen()
    {
        var view = Load("MainWindow.xaml").Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == "_View");
        var items = view.Elements(Presentation + "MenuItem").ToList();
        var group = items.Single(item => (string?)item.Attribute("Header") == "_Group project by");

        Assert.That(items.IndexOf(group), Is.EqualTo(items.FindIndex(item => (string?)item.Attribute("Header") == "Rows b_y") + 1));
        Assert.That((string?)group.Attribute("Visibility"), Is.EqualTo("{Binding IsProjectOpen, Converter={StaticResource BoolToVisibility}}"));
        Assert.That(group.Elements(Presentation + "MenuItem").Select(item =>
            ((string?)item.Attribute("Header"), (string?)item.Attribute("IsCheckable"), (string?)item.Attribute("IsChecked"))), Is.EqualTo(new[]
        {
            ("_Flow", "True", "{Binding ProjectView.IsGroupedByFlow, Mode=TwoWay}"),
            ("_Objectives", "True", "{Binding ProjectView.IsGroupedByObjectives, Mode=TwoWay}"),
        }));
    }

    [Test]
    public void BothGroupings_DrawTheSameBox()
    {
        var presenters = View().Descendants(Presentation + "ContentPresenter")
            .Where(presenter => (string?)presenter.Attribute("ContentTemplate") == "{StaticResource PieceBox}").ToList();

        Assert.That(presenters, Has.Count.EqualTo(2), "the Flow's node and the box under an objective");
        Assert.That(View().Descendants(Presentation + "DataTemplate").Count(each => (string?)each.Attribute(Xaml + "Key") == "PieceBox"), Is.EqualTo(1));
        Assert.That((string?)Named(View(), "ObjectivePieces").Attribute("ItemTemplate"), Is.EqualTo("{StaticResource ObjectivePiece}"));
    }

    [TestCase("PhaseHeader")]
    [TestCase("ObjectiveHeader")]
    public void AHeading_SelectsWithAClickEnterOrSpace_AndHasItsMenu(string header)
    {
        var element = Named(View(), header);
        var menu = element.Descendants(Presentation + "ContextMenu").Single();

        Assert.That((string?)element.Attribute("Focusable"), Is.EqualTo("True"));
        Assert.That(element.Descendants(Presentation + "KeyBinding").Select(binding => (string?)binding.Attribute("Key")),
            Is.EquivalentTo(new[] { "Enter", "Space" }));
        Assert.That((string?)element.Descendants(Presentation + "MouseBinding").Single().Attribute("Command"), Is.EqualTo("{Binding SelectCommand}"));
        Assert.That(((string?)menu.Attribute("ItemsSource"), (string?)menu.Attribute("ItemContainerStyle")),
            Is.EqualTo(("{Binding MenuItems}", "{StaticResource StageMenuItem}")));
    }

    [Test]
    public void TheOverlay_IsThePane_MovedOverTheFlowAtItsMinimumWidth_WithAClosingCross()
    {
        var view = View();
        var pane = Named(view, "Pane");
        var trigger = pane.Descendants(Presentation + "DataTrigger").Single();
        string? Setter(string property) =>
            (string?)trigger.Elements(Presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == property).Attribute("Value");
        var close = Named(view, "CloseOverlay");

        Assert.That((string?)trigger.Attribute("Binding"), Is.EqualTo("{Binding IsPaneDocked}"));
        Assert.That((Setter("Grid.ColumnSpan"), Setter("HorizontalAlignment"), Setter("Width")),
            Is.EqualTo(("3", "Right", "{x:Static vm:ProjectPaneWidth.Minimum}")));
        Assert.That(pane.Descendants(Presentation + "Condition").Select(condition => (string?)condition.Attribute("Binding")),
            Is.EqualTo(new[] { "{Binding IsPaneDocked}", "{Binding IsOverlayOpen}" }));
        Assert.That(((string?)close.Attribute("Content"), (string?)close.Attribute("Command"), (string?)close.Attribute("ToolTip")),
            Is.EqualTo(("×", "{Binding CloseOverlayCommand}", "Close Properties (Esc)")));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
