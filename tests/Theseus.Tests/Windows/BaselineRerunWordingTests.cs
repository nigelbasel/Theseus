using System.Xml.Linq;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Wording and keys the 1.9 baseline's re-run found after the fix batches (WB9; checklist G33, G36, G82, G84, G95, G100,
/// G102, G107): labels before their control end in a colon, a command that only opens a window has no ellipsis, one
/// command keeps one name, and an access key is not used twice in the main window.
/// </summary>
[TestFixture]
public class BaselineRerunWordingTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestCase("Show:")]
    [TestCase("Show cards with:")]
    public void ALabelBeforeItsControl_EndsInAColon(string label) =>
        Assert.That(Texts("MainWindow.xaml", "TextBlock", "Text"), Does.Contain(label), "NA-2, W20, G100");

    [Test]
    public void EditSetup_OnlyOpensAWindow_SoHasNoEllipsis() =>
        Assert.That(Texts("MainWindow.xaml", "Button", "Content"), Does.Contain("Edit setup").And.No.Contain("Edit setup…"), "NA-3, W14");

    [Test]
    public void IncludeArchived_HasNoKey_SinceEveryLetterOfItIsTakenWhereItShows() =>
        Assert.That(Element("MainWindow.xaml", "IncludeArchived").Attribute("Content")!.Value, Is.EqualTo("Include archived"),
            "NB-3, G84: A is Forward's, N is Start without their inputs' and Rename objective's, I and O are not used (W26)");

    [Test]
    public void TheRoundsStateWord_IsSemibold_NotMedium() =>
        Assert.That(Element("Views/ProjectView.xaml", "RoundState").Attribute("FontWeight")!.Value, Is.EqualTo("SemiBold"), "NA-4, G95");

    [Test]
    public void RemoveFromProject_AsksNothing_SoHasNoEllipsisInTheProjectView() =>
        Assert.That(File.ReadAllText(LooseXaml.SourceFile("ViewModels", "ShellViewModel.cs")), Does.Not.Contain("\"Remove from project…\""), "C13, W14");

    [TestCase("ProjectPropertiesWindow.xaml", "The title in its project.md's first line")]
    [TestCase("ProjectPropertiesWindow.xaml", "A tag: letters, digits and hyphens")]
    [TestCase("ProjectPropertiesWindow.xaml", "Forget the folder you chose")]
    [TestCase("ProjectPropertiesWindow.xaml", "Choose this project's holdout folder")]
    [TestCase("ProjectPropertiesWindow.xaml", "Add your own private repository")]
    [TestCase("SettingsWindow.xaml", "Show this folder's project card again")]
    [TestCase("SettingsWindow.xaml", "The tag's new name")]
    [TestCase("MainWindow.xaml", "Filter the board by tags")]
    [TestCase("Views/ProjectView.xaml", "Show every line of Waits for you")]
    public void ASentenceTooltip_EndsWithAFullStop(string file, string start) =>
        Assert.That(Load(file).Descendants().Select(element => (string?)element.Attribute("ToolTip")).Single(tip => tip?.StartsWith(start, StringComparison.Ordinal) == true),
            Does.EndWith("."), "A22, G106, G107");

    [Test]
    public void AcceptSpecsTooltip_EndsWithAFullStop() =>
        Assert.That(Theseus.App.ViewModels.ProjectViewModel.AcceptSpecToolTip, Does.EndWith("."), "A22");

    private static XElement Element(string file, string name) =>
        Load(file).Descendants().Single(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == name);

    private static List<string> Texts(string file, string element, string attribute) =>
        [.. Load(file).Descendants(Presentation + element).Select(each => (string?)each.Attribute(attribute)).Where(text => text is not null).Select(text => text!)];

    private static XDocument Load(string file) => XDocument.Load(LooseXaml.SourceFile(file));
}
