using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Bring the window forward is in the project view and on the menu bar (project-view AC 21; rubric R16), and says why
/// when it cannot act, in a tooltip shown on the disabled command.
/// </summary>
[TestFixture]
public class ProjectWindowMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XName Name = XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml");
    private static readonly XName ShowOnDisabled = XName.Get("ToolTipService.ShowOnDisabled");

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    [Test]
    public void TheProjectView_HasBringWindowForward_WithItsReasonOnTheDisabledButton()
    {
        var button = Load("Views", "ProjectView.xaml").Descendants(Presentation + "Button")
            .Single(element => (string?)element.Attribute(Name) == "BringWindowForward");

        Assert.That((string?)button.Attribute("Command"), Does.Contain("BringProjectWindowForwardCommand"));
        Assert.That((string?)button.Attribute("ToolTip"), Does.Contain("ProjectWindowTooltip"));
        Assert.That((string?)button.Attribute(ShowOnDisabled), Is.EqualTo("True"));
    }

    [Test]
    public void TheViewMenu_HasBringProjectWindowForward_ShownWhileAProjectIsOpen()
    {
        var item = Load("MainWindow.xaml").Descendants(Presentation + "MenuItem")
            .Single(element => (string?)element.Attribute("Header") == "_View")
            .Descendants(Presentation + "MenuItem")
            .Single(element => (string?)element.Attribute("Command") == "{Binding BringProjectWindowForwardCommand}");

        Assert.That((string?)item.Attribute("Visibility"), Is.EqualTo("{Binding IsProjectOpen, Converter={StaticResource BoolToVisibility}}"));
        Assert.That((string?)item.Attribute("ToolTip"), Is.EqualTo("{Binding ProjectWindowTooltip}"));
        Assert.That((string?)item.Attribute(ShowOnDisabled), Is.EqualTo("True"));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
