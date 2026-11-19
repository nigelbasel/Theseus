using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>Verify with holdouts… is in the card's menu and the Workstream menu (verifier-card AC 9; rubric R16).</summary>
[TestFixture]
public class VerifyMenuTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheCardsMenu_OffersVerifyWithHoldouts()
    {
        var cardMenu = Window().Descendants(Presentation + "ContextMenu")
            .Single(menu => menu.Descendants(Presentation + "MenuItem")
                .Any(item => (string?)item.Attribute("Command") == "{Binding SpawnFollowOnCommand}"));

        Assert.That(cardMenu.Descendants(Presentation + "MenuItem")
            .Any(item => (string?)item.Attribute("Command") == "{Binding VerifyWithHoldoutsCommand}"), Is.True);
    }

    [Test]
    public void TheWorkstreamMenu_OffersVerifyWithHoldouts()
    {
        var workstreamMenu = Window().Descendants(Presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == "_Workstream");

        Assert.That(workstreamMenu.Descendants(Presentation + "MenuItem")
            .Any(item => (string?)item.Attribute("Command") == "{Binding VerifySelectedWithHoldoutsCommand}"), Is.True);
    }

    private static XDocument Window() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
