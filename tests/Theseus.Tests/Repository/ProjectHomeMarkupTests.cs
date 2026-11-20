using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// The markup of 1.1b pull request 2: Project › New project… shows only once a projects folder is set (AC 7, 13); the
/// Settings panel that explains the model shows until one is saved, with both Choose buttons and the help link (AC 11);
/// the help page it and the Help menu name is in the docs (AC 12). The markup alone: what is rendered is for the live
/// check (P11).
/// </summary>
[TestFixture]
public class ProjectHomeMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheProjectMenu_OffersNewProject_OnlyOnceAProjectsFolderIsSet()
    {
        var project = Load("MainWindow.xaml").Descendants(Presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == "_Project");

        Assert.That((string?)project.Attribute("Visibility"),
            Is.EqualTo("{Binding HasProjectsFolder, Converter={StaticResource BoolToVisibility}}"));
        Assert.That(project.Elements(Presentation + "MenuItem").Select(item => (string?)item.Attribute("Command")),
            Is.EqualTo(new[] { "{Binding NewProjectCommand}" }));
    }

    [Test]
    public void SettingsExplainsTheModel_UntilAProjectsFolderIsSaved_WithBothChoosersAndTheHelpLink()
    {
        var panel = Load("SettingsWindow.xaml").Descendants(Presentation + "Border")
            .Single(border => (string?)border.Attribute("Visibility") == "{Binding ShowsProjectsIntro, Converter={StaticResource BoolToVisibility}}");

        Assert.That(panel.Descendants(Presentation + "Button").Select(button => (string?)button.Attribute("Click")),
            Is.EqualTo(new[] { "OnChooseProjectsFolder", "OnChooseWorktreesFolder" }));
        Assert.That((string?)panel.Descendants(Presentation + "Hyperlink").Single().Attribute("NavigateUri"),
            Is.EqualTo("https://nigelbasel.github.io/Theseus/project-layout.html"));
    }

    [Test]
    public void TheHelpPage_ThePanelAndTheHelpMenuName_IsInTheDocs_AndListedInTheContents()
    {
        var help = Load("MainWindow.xaml").Descendants(Presentation + "MenuItem")
            .Single(item => (string?)item.Attribute("Header") == "How your work is _laid out");

        Assert.That((string?)help.Attribute("CommandParameter"), Is.EqualTo("project-layout"));
        Assert.That(File.Exists(Path.Combine(RepositoryRoot(), "docs", "project-layout.md")), Is.True);
        Assert.That(File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "index.md")), Does.Contain("(project-layout.md)"));
    }

    private static XDocument Load(string file) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", file));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
