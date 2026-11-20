using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Project is on the card's menu and the Workstream menu (project-view AC 4; rubric R16), and each is shown only while
/// a project is readable, so a board with no projects has no new item (AC 22). The markup alone: what is rendered is
/// for the verifier's live check (P11).
/// </summary>
[TestFixture]
public class ProjectMenuMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheCardsMenu_OffersProject_OnlyWhileThereAreChoices()
    {
        var cardMenu = Window().Descendants(Presentation + "ContextMenu")
            .Single(menu => menu.Descendants(Presentation + "MenuItem")
                .Any(item => (string?)item.Attribute("Command") == "{Binding SpawnFollowOnCommand}"));

        AssertProjectItem(cardMenu);
    }

    [Test]
    public void TheWorkstreamMenu_OffersProject_OnlyWhileThereAreChoices()
    {
        var workstreamMenu = Window().Descendants(Presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == "_Workstream");

        AssertProjectItem(workstreamMenu);
    }

    private static void AssertProjectItem(XElement menu)
    {
        var project = menu.Descendants(Presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("ItemsSource") == "{Binding ProjectChoices}");

        Assert.That((string?)project.Attribute("Visibility"),
            Is.EqualTo("{Binding HasProjectChoices, Converter={StaticResource BoolToVisibility}}"));
        Assert.That((string?)project.Attribute("ItemContainerStyle"), Is.EqualTo("{StaticResource ProjectChoiceItem}"));
    }

    private static XDocument Window() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
