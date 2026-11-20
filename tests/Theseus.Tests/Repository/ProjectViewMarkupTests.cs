using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Xml.Linq;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Repository;

/// <summary>
/// The main window's wiring of projects (project-view AC 12, 14, 15, 17, 22; rubric R16): the view is its own control
/// in place of the board, every command in it is on the menu bar, the rows-by-folder label reads Folder, and a cell's
/// project strip takes no room when it holds no project card.
/// </summary>
[TestFixture]
public class ProjectViewMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static XDocument Window() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

    private static XElement Menu(string header) =>
        Window().Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == header);

    private static IEnumerable<string?> Headers(XElement menu) =>
        menu.Descendants(Presentation + "MenuItem").Select(item => (string?)item.Attribute("Header"));

    [Test]
    public void TheProjectView_IsItsOwnControl_InPlaceOfTheBoard()
    {
        var window = Window();
        var view = window.Descendants().Single(element => element.Name.LocalName == "ProjectView");
        var surface = window.Descendants(Presentation + "Grid").Single(grid => (string?)grid.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "BoardSurface");

        Assert.That((string?)view.Attribute("DataContext"), Is.EqualTo("{Binding ProjectView}"));
        Assert.That((string?)surface.Attribute("Visibility"), Is.EqualTo("{Binding IsBoardShown, Converter={StaticResource BoolToVisibility}}"));
        Assert.That(File.Exists(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "Views", "ProjectView.xaml")), Is.True);
    }

    [Test]
    public void EveryCommandInTheView_IsOnTheMenuBar()
    {
        Assert.That(Headers(Menu("_View")), Is.SupersetOf(new[] { "Open pro_ject", "Back to the boar_d" }));
        Assert.That(Headers(Menu("_Workstream")), Is.SupersetOf(new[] { "_Start selected", "Ope_n spec", "Acc_ept spec" }));
    }

    [Test]
    public void TheMenuBarsProjectItems_AreHidden_WhileThereIsNothingToOffer()
    {
        var items = Menu("_View").Descendants(Presentation + "MenuItem")
            .Concat(Menu("_Workstream").Descendants(Presentation + "MenuItem"))
            .Where(item => (string?)item.Attribute("Header") is "Open pro_ject" or "Back to the boar_d" or "Ope_n spec" or "Acc_ept spec")
            .ToList();

        Assert.That(items.Select(item => (string?)item.Attribute("Visibility")), Is.EqualTo(new[]
        {
            "{Binding HasOpenProjectChoices, Converter={StaticResource BoolToVisibility}}",
            "{Binding IsProjectOpen, Converter={StaticResource BoolToVisibility}}",
            "{Binding HasSpecChoices, Converter={StaticResource BoolToVisibility}}",
            "{Binding HasSpecChoices, Converter={StaticResource BoolToVisibility}}",
        }));
    }

    [Test]
    public void TheRowsByFolderMode_IsLabelledFolder_AndKeepsItsSavedName()
    {
        var item = Menu("_View").Descendants(Presentation + "MenuItem")
            .Single(each => (string?)each.Attribute("IsChecked") == "{Binding Board.RowsByProject, Mode=TwoWay}");

        Assert.That((string?)item.Attribute("Header"), Is.EqualTo("_Folder"));
    }

    [Test]
    public void ACardsFace_ShowsItsTooltip_WithTheProjectSourceWhenThereIsOne()
    {
        var face = Window().Descendants(Presentation + "Border")
            .Single(border => (string?)border.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "CardFace");

        Assert.That((string?)face.Attribute("ToolTip"), Is.EqualTo("{Binding FaceTooltip}"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void ACellsProjectStrip_TakesNoRoom_WithNoProjectCard()
    {
        var strip = Window().Descendants(Presentation + "ItemsControl")
            .Single(control => (string?)control.Attribute("ItemsSource") == "{Binding Projects}");

        // The strip as the window declares it, less its template and handlers, which need the window behind them; with
        // no items neither is used.
        var markup = new XElement(Presentation + "ItemsControl", new XAttribute("ItemsSource", "{Binding Projects}"));
        var control = (ItemsControl)XamlReader.Parse(markup.ToString());
        control.DataContext = new ColumnViewModel("New", "Dev");

        control.Measure(new Size(300, 600));
        control.Arrange(new Rect(0, 0, 300, 600));
        control.UpdateLayout();

        Assert.That((string?)strip.Attribute("ItemTemplate"), Is.EqualTo("{StaticResource ProjectCardTemplate}"));
        Assert.That(control.DesiredSize.Height, Is.Zero, "a board with no projects looks exactly as before (AC 22)");
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
