using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// The main window's wiring of the project card (1.1d AC 40 to 43): it is clicked, right-clicked and dropped as a card
/// is, Enter opens it, its menu starts, stops and shows its properties, and the commands for one workstream say why
/// they are refused while a project is selected.
/// </summary>
[TestFixture]
public class ProjectCardMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Window() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

    private static XElement Face() =>
        Window().Descendants(Presentation + "Border").Single(border => (string?)border.Attribute(Xaml + "Name") == "ProjectFace");

    [Test]
    public void AProjectCard_IsClickedAndRightClickedAsACardIs_AndEnterOpensIt()
    {
        var face = Face();
        var enter = face.Descendants(Presentation + "KeyBinding").Single(binding => (string?)binding.Attribute("Key") == "Return");

        Assert.That(((string?)face.Attribute("MouseLeftButtonUp"), (string?)face.Attribute("PreviewMouseRightButtonDown")),
            Is.EqualTo(("OnProjectCardClicked", "OnProjectCardRightButtonDown")));
        Assert.That(((string?)face.Attribute("Tag"), (string?)face.Attribute("Focusable")), Is.EqualTo(("ProjectCard", "True")),
            "the rubber band finds it by its tag; Enter needs focus on it");
        Assert.That(((string?)enter.Attribute("Key"), (string?)enter.Attribute("Command")), Is.EqualTo(("Return", "{Binding OpenCommand}")));
    }

    [Test]
    public void ItsMenu_Opens_StartsStops_AndShowsProjectProperties()
    {
        var headers = Face().Descendants(Presentation + "MenuItem")
            .Select(item => ((string?)item.Attribute("Header"), (string?)item.Attribute("Command")))
            .ToList();

        Assert.That(headers, Is.EqualTo(new (string?, string?)[]
        {
            ("_Open", "{Binding OpenCommand}"),
            ("_Update", "{Binding OpenCommand}"),
            ("_Not a project", "{Binding HideCommand}"),
            ("_Start", "{Binding StartCommand}"),
            ("S_top", "{Binding StopCommand}"),
            ("T_ags", null),
            ("Open project._md", "{Binding OpenFileCommand}"),
            ("Add o_bjective…", "{Binding AddObjectiveCommand}"),
            ("Project p_roperties", "{Binding PropertiesCommand}"),
        }), "Properties last, G51 (baseline re-run NC-7)");
    }

    [Test]
    public void ACellsProjectStrip_TakesADrop_AsTheCellsCardsDo()
    {
        var strip = Window().Descendants(Presentation + "ItemsControl").Single(control => (string?)control.Attribute(Xaml + "Name") == "ProjectCards");

        Assert.That(((string?)strip.Attribute("AllowDrop"), (string?)strip.Attribute("Drop"), (string?)strip.Attribute("DragOver")),
            Is.EqualTo(("True", "OnProjectsDrop", "OnColumnDragOver")));
    }

    [Test]
    public void CommandsForOneWorkstream_SayWhyTheyAreRefusedForAProject()
    {
        var items = Window().Descendants(Presentation + "MenuItem")
            .Where(item => (string?)item.Attribute("Command") is "{Binding PropertiesCommand}" or "{Binding VerifySelectedWithHoldoutsCommand}")
            .Where(item => item.Ancestors(Presentation + "Menu").Any())
            .ToList();

        Assert.That(items.Select(item => ((string?)item.Attribute("ToolTip"), (string?)item.Attribute("ToolTipService.ShowOnDisabled"))),
            Is.EquivalentTo(new (string?, string?)[]
            {
                ("{Binding PropertiesToolTip}", "True"),
                ("{Binding VerifySelectedToolTip}", "True"),
            }));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
