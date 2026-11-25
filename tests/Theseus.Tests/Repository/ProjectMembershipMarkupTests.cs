using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Membership is read from the project files, so no menu sets it any more (1.1c AC 3); and a project card can be dimmed,
/// with its Open saying what it does (AC 5). The markup alone.
/// </summary>
[TestFixture]
public class ProjectMembershipMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void NoMenuSetsACardsProject()
    {
        var items = Window().Descendants(Presentation + "MenuItem").Select(item => (string?)item.Attribute("ItemsSource"));

        Assert.That(items, Has.None.EqualTo("{Binding ProjectChoices}"));
    }

    [Test]
    public void AProjectCard_CanBeDimmed_AndItsOpenSaysWhatItDoes()
    {
        var face = Window().Descendants(Presentation + "Border").Single(border => (string?)border.Attribute("Tag") == "ProjectCard");

        Assert.That((string?)face.Attribute("Opacity"), Is.EqualTo("{Binding CardOpacity}"));
        Assert.That(face.Descendants(Presentation + "MenuItem").Select(item => (string?)item.Attribute("ToolTip")),
            Has.Member("{Binding OpenToolTip}"));
    }

    private static XDocument Window() =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", "MainWindow.xaml"));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
