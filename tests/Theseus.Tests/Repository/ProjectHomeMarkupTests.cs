using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// The markup of 1.1b pull request 2, as 1.1c moved it: New project… sits beside New workstream, in the File menu and on
/// the toolbar, only once a projects folder is set (1.1c AC 6; 1.1b AC 13); the
/// Settings panel that explains the model shows until one is saved, with both Choose buttons and the help link (AC 11);
/// the help page it and the Help menu name is in the docs (AC 12). The markup alone: what is rendered is for the live
/// check (P11).
/// </summary>
[TestFixture]
public class ProjectHomeMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void NewProject_SitsBesideNewWorkstream_DisabledWithItsReasonUntilAProjectsFolderIsSet()
    {
        var window = Load("MainWindow.xaml");
        var file = window.Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Header") == "_File");
        var newProject = window.Descendants().Where(element => (string?)element.Attribute("Command") == "{Binding NewProjectCommand}").ToList();

        Assert.That(newProject.Select(element => element.Name.LocalName), Is.EquivalentTo(new[] { "MenuItem", "Button" }), "the File menu and the toolbar");
        // Greyed with its reason, not hidden (W31, C8).
        Assert.That(newProject.Select(element => (string?)element.Attribute("Visibility")), Is.All.Null);
        Assert.That(newProject.Select(element => (string?)element.Attribute("IsEnabled")), Is.All.EqualTo("{Binding HasProjectsFolder}"));
        Assert.That(newProject.Select(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "OffReason.Text").Value),
            Has.All.Contain("projects folder"));
        Assert.That(file.Elements(Presentation + "MenuItem").Select(item => (string?)item.Attribute("Command")),
            Does.Contain("{Binding NewProjectCommand}"));
        Assert.That(window.Descendants(Presentation + "MenuItem").Select(item => (string?)item.Attribute("Header")),
            Has.None.EqualTo("_Project"), "no menu of its own any more");
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
