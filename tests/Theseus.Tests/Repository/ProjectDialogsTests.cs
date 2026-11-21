using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Colour;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Repository;

/// <summary>
/// A project's dialog, as WPF lays it out: Project properties, the title read only, the colour chosen from the project
/// palette, and the project.md path (1.1d AC 40). Start's confirmation is gone: a project's Start starts its run state
/// with no list to tick (1.1e AC 9).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProjectDialogsTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static FrameworkElement LaidOut(Window window)
    {
        var content = (FrameworkElement)window.Content;

        content.Measure(new Size(520, 900));
        content.Arrange(new Rect(0, 0, 520, content.DesiredSize.Height));
        content.UpdateLayout();

        return content;
    }

    [Test]
    public void ProjectProperties_ShowsTheTitleToEdit_TheProjectPalette_AndTheProjectFileReadOnly()
    {
        var properties = new ProjectPropertiesViewModel(TheseusProject, "Theseus bootstrap", "#5E4BB0", @"C:\projects	heseus\project.md");
        var window = new ProjectPropertiesWindow(properties);
        LaidOut(window);
        var colours = (ListBox)window.FindName("ColourBox");

        Assert.That(((TextBox)window.FindName("TitleBox")).IsReadOnly, Is.False, "the title is edited here (1.1e AC 12a)");
        Assert.That(((TextBox)window.FindName("FileBox")).IsReadOnly, Is.True);
        Assert.That(((TextBox)window.FindName("FileBox")).ToolTip as string, Does.Contain("Read only"));
        Assert.That(((TextBox)window.FindName("FileBox")).Text, Is.EqualTo(@"C:\projects	heseus\project.md"));
        Assert.That(colours.Items.Cast<ColourSwatchViewModel>().Select(swatch => swatch.Colour), Is.EqualTo(ProjectColourPalette.All));
        Assert.That(colours.SelectedValue, Is.EqualTo("#5E4BB0"));

        colours.SelectedValue = "#2F7D32";

        Assert.That(properties.ColourHex, Is.EqualTo("#2F7D32"));
    }

    [Test]
    public void AColourAnotherProjectHolds_IsMarked_AndNamesIt_ButCanStillBeChosen()
    {
        var properties = new ProjectPropertiesViewModel(TheseusProject, "Theseus bootstrap", "#5E4BB0", "project.md",
            new Dictionary<string, string> { ["BuildYourMarket MCP server"] = "#2F7D32" });
        var window = new ProjectPropertiesWindow(properties);
        var laidOut = LaidOut(window);
        var taken = properties.Colours.Single(swatch => swatch.Colour == "#2F7D32");
        var marks = Descendants<TextBlock>(laidOut).Where(text => text.Name == "TakenMark").ToList();

        Assert.That((taken.IsTaken, taken.ToolTip), Is.EqualTo((true, "#2F7D32, used by BuildYourMarket MCP server")));
        Assert.That(properties.Colours.Count(swatch => swatch.IsTaken), Is.EqualTo(1));
        Assert.That(marks.Count(mark => mark.Visibility == Visibility.Visible), Is.EqualTo(1));

        ((ListBox)window.FindName("ColourBox")).SelectedValue = "#2F7D32";

        Assert.That(properties.ColourHex, Is.EqualTo("#2F7D32"));
    }

    [Test]
    public void AColourFromOutsideThePalette_IsOfferedFirst_SoItIsNotLostByOpeningTheDialog()
    {
        var properties = new ProjectPropertiesViewModel("p", "P", "#123456", "project.md");

        Assert.That(properties.Colours[0].Colour, Is.EqualTo("#123456"));
        Assert.That(properties.Colours.Skip(1).Select(swatch => swatch.Colour), Is.EqualTo(ProjectColourPalette.All));
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
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
