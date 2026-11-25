using System.Xml.Linq;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// Theme and naming the 1.9 baseline's re-run found after the fix batches (WB9; checklist G161, G162, G166, G167,
/// G169; decisions W15, W16, W47, W48, W50): the link colour only on links, secondary rather than tertiary text where it
/// informs, a count says what it counts, a selector group says what it switches, and a link looks like one.
/// </summary>
[TestFixture]
public class BaselineRerunThemeTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void TheLinkColour_IsNotABordersHighlight() =>
        Assert.That(Load("Views/ProjectView.xaml").Descendants(Presentation + "Setter")
            .Where(setter => (string?)setter.Attribute("Property") == "BorderBrush" && (string?)setter.Attribute("Value") == "{DynamicResource LinkBrush}"),
            Is.Empty, "E2, W47: under a contrast theme LinkBrush is the hot-track colour, which says link");

    [Test]
    public void AChainsRowsStateWord_IsSecondaryText() =>
        Assert.That(Load("MainWindow.xaml").Descendants(Presentation + "TextBlock").Single(text => (string?)text.Attribute("Text") == "{Binding StateText}"
            && text.Descendants(Presentation + "DataTrigger").Any(trigger => (string?)trigger.Attribute("Binding") == "{Binding RowState}"))
            .Descendants(Presentation + "Setter").First(setter => (string?)setter.Attribute("Property") == "Foreground").Attribute("Value")!.Value,
            Is.EqualTo("{DynamicResource TextFillColorSecondaryBrush}"), "E-m1, W48, G161");

    [TestCase("{Binding CountText}", "{Binding WaitingText}")]
    [TestCase("{Binding MatchCount}", "{Binding MatchText}")]
    public void ABareCount_IsNamedByWhatItCounts(string text, string name) =>
        Assert.That(Load("MainWindow.xaml").Descendants(Presentation + "TextBlock").Where(each => (string?)each.Attribute("Text") == text)
            .Select(each => (string?)each.Attribute("AutomationProperties.Name")), Is.All.EqualTo(name), "NE-7, G167");

    [TestCase("ChainsPaneSwitch", "Views of the side panel")]
    [TestCase("LocksPaneSwitch", "Views of the side panel")]
    [TestCase("BoardShowsChoice", "Show")]
    [TestCase("TagMatch", "Show cards with")]
    public void ASelectorGroup_SaysWhatItSwitches(string group, string name) =>
        Assert.That(Element("MainWindow.xaml", group).Attribute("AutomationProperties.Name")?.Value, Is.EqualTo(name), "NE-8, G169");

    [TestCase("{Binding Drawer.ShowLinksCommand}")]
    [TestCase("{Binding ShowOtherCommand}")]
    public void AButtonThatOnlyNavigates_InTheDetails_IsALink(string command) =>
        Assert.That(Load("MainWindow.xaml").Descendants(Presentation + "Button").Single(button => (string?)button.Attribute("Command") == command)
            .Attribute("Style")?.Value, Is.EqualTo("{StaticResource LinkButton}"), "NE-1, W15, W16, G166");

    private static XElement Element(string file, string name) =>
        Load(file).Descendants().Single(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == name);

    private static XDocument Load(string file) => XDocument.Load(LooseXaml.SourceFile(file));
}
