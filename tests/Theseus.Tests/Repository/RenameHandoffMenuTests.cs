using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>A card's menu offers to rename its handover file, shown only when the card offers it (handoff-names AC 5).</summary>
[TestFixture]
public class RenameHandoffMenuTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheCardsMenu_OffersRenameHandover_OnlyWhenOffered()
    {
        var window = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));
        var cardMenu = window.Descendants(Presentation + "ContextMenu")
            .Single(menu => menu.Descendants(Presentation + "MenuItem")
                .Any(item => (string?)item.Attribute("Command") == "{Binding SpawnFollowOnCommand}"));

        var rename = cardMenu.Descendants(Presentation + "MenuItem")
            .SingleOrDefault(item => (string?)item.Attribute("Command") == "{Binding RenameHandoffCommand}");

        Assert.That(rename, Is.Not.Null);
        Assert.That((string?)rename!.Attribute("Visibility"), Does.Contain("HandoffRenameTo"));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
