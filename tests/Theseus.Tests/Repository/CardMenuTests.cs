using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// A command on a card is in the card's menu as well as on its buttons (rubric R16). Close now was a button only
/// until the close-step review (S2; Nigel, 2026-11-19).
/// </summary>
[TestFixture]
public class CardMenuTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheCardsMenu_OffersCloseNow_OnlyWhenTheCardOffersIt()
    {
        var closeNow = CardMenuItems().SingleOrDefault(item => (string?)item.Attribute("Command") == "{Binding CloseNowCommand}");

        Assert.That(closeNow, Is.Not.Null, "the card's context menu has Close now");
        Assert.That((string?)closeNow!.Attribute("Visibility"), Does.Contain("CanCloseNow"),
            "it shows exactly when the card's own Close now button does");
    }

    private static IEnumerable<XElement> CardMenuItems()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

        var cardMenu = window.Descendants(Presentation + "ContextMenu")
            .Single(menu => menu.Descendants(Presentation + "MenuItem")
                .Any(item => (string?)item.Attribute("Command") == "{Binding SpawnFollowOnCommand}"));

        return cardMenu.Descendants(Presentation + "MenuItem");
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
