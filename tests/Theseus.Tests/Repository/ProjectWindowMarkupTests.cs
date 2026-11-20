using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Bring the window forward is on the menu bar (project-view AC 21; rubric R16), and says why when it cannot act, in a
/// tooltip shown on the disabled command. The project view's own button went in 1.1d (AC 16).
/// </summary>
[TestFixture]
public class ProjectWindowMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XName ShowOnDisabled = XName.Get("ToolTipService.ShowOnDisabled");

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    // Changed by 1.1d AC 16: the header's Bring window forward button is removed; View › Bring project window forward stays.
    [Test]
    public void TheProjectView_HasNoBringWindowForwardButton_ItIsOnTheViewMenuOnly()
    {
        var view = Load("Views", "ProjectView.xaml");

        Assert.That(view.Descendants().Attributes().Select(attribute => attribute.Value),
            Has.None.Contains("BringProjectWindowForwardCommand"));
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
