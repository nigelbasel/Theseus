using System.Xml.Linq;
using Theseus.App.ViewModels;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Commands the 1.9 baseline's re-run found after the fix batches (WB9; checklist G51, G88, G90, G150, G157; decisions
/// W13, W25, W29, W49): a disabled command says why, a fold chevron shows the state as every other one does, a note that
/// reports the user's own choice is information, not a warning, and Properties ends a menu whose default is Open.
/// </summary>
[TestFixture]
public class BaselineRerunCommandsTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void Marking_SaysWhyNot_WhenNothingIsTicked_OrEveryTickedCardIsMarked()
    {
        Assert.That(SelectionReasons.Mark(0, 0, "paused"), Is.EqualTo(SelectionReasons.NothingTicked), "NC-2, G157");
        Assert.That(SelectionReasons.Mark(2, 0, "blocked"), Is.EqualTo("Every ticked card is already blocked."));
        Assert.That(SelectionReasons.Mark(2, 1, "paused"), Is.Null);
        Assert.That(SelectionReasons.ClearMarks(2, 0), Is.EqualTo("No ticked card is paused or blocked."));
        Assert.That(SelectionReasons.ClearMarks(2, 1), Is.Null);
    }

    [TestCase("Mark as _paused", "{Binding MarkSelectedPausedCommand.WhyNot}")]
    [TestCase("Mark as _blocked", "{Binding MarkSelectedBlockedCommand.WhyNot}")]
    [TestCase("_Clear paused and blocked", "{Binding ClearSelectedMarksCommand.WhyNot}")]
    public void AMarkItem_ExplainsWhenOff(string header, string reason)
    {
        XElement item = Window().Descendants(Presentation + "MenuItem").Single(each => (string?)each.Attribute("Header") == header);

        Assert.That(((string?)item.Attribute("Style"), (string?)item.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "OffReason.Text")),
            Is.EqualTo(("{StaticResource ExplainsWhenOff}", reason)), "NC-2, W13");
    }

    [TestCase("{Binding DataContext.FocusCardCommand, RelativeSource={RelativeSource AncestorType=Window}}")]
    [TestCase("{Binding FocusCardCommand}")]
    public void AFocusButton_SaysWhyItIsOff(string command)
    {
        XElement button = Window().Descendants(Presentation + "Button").Single(each => (string?)each.Attribute("Command") == command);
        string key = ((string?)button.Attribute("Style"))!.Replace("{StaticResource ", string.Empty, StringComparison.Ordinal).TrimEnd('}');
        XElement style = Window().Descendants(Presentation + "Style").Single(each => Key(each) == key);

        Assert.That(style.Descendants(Presentation + "Setter").Any(setter => (string?)setter.Attribute("Property") == "ToolTipService.ShowOnDisabled"), Is.True, "NC-1, G157");
        Assert.That(style.Descendants(Presentation + "Trigger").Where(trigger => (string?)trigger.Attribute("Property") == "IsEnabled")
            .Descendants(Presentation + "Setter").Select(setter => (string?)setter.Attribute("Property")), Does.Contain("ToolTip"));
    }

    [Test]
    public void SomeCardsHidden_IsInformation_InSecondaryText()
    {
        XElement glyph = Element("ShowHides").Elements().First();

        Assert.That(((string?)glyph.Attribute("Text"), (string?)glyph.Attribute("Foreground")),
            Is.EqualTo(("\uE946", "{DynamicResource TextFillColorSecondaryBrush}")), "NC-4, G150, W49");
    }

    [Test]
    public void ACardsFoldChevron_ShowsItsState_AsARowsDoes()
    {
        XElement style = Window().Descendants(Presentation + "Style").Single(each => Key(each) == "CardFoldButton");

        Assert.That(style.Elements(Presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "Content").Attribute("Value")!.Value,
            Is.EqualTo("\uE70D"), "NC-5, W25: open");
        Assert.That(style.Descendants(Presentation + "DataTrigger").Descendants(Presentation + "Setter")
            .Single(setter => (string?)setter.Attribute("Property") == "Content").Attribute("Value")!.Value, Is.EqualTo("\uE76C"), "folded");
        Assert.That(Glyph("Fold card"), Is.EqualTo("\uE70D"));
        Assert.That(Glyph("Unfold card"), Is.EqualTo("\uE76C"));
    }

    [Test]
    public void AProjectCardsMenu_EndsWithProjectProperties_SinceOpenIsItsDefault()
    {
        XElement menu = Window().Descendants(Presentation + "ContextMenu").First(each => each.Elements(Presentation + "MenuItem")
            .Any(item => ((string?)item.Attribute("Header"))?.Contains("p_roperties", StringComparison.Ordinal) == true));

        Assert.That((string?)menu.Elements().Last().Attribute("Header"), Is.EqualTo("Project p_roperties"),
            "NC-7, G51: primary first, Properties last; W29's deviation is for menus whose default is Properties");
    }

    private static string? Glyph(string name) =>
        Window().Descendants(Presentation + "Button").Single(button => (string?)button.Attribute("AutomationProperties.Name") == name)
            .Descendants().First(element => element.Name.LocalName == "GlyphText").Attribute("Text")?.Value;

    private static string? Key(XElement element) => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key");

    private static XElement Element(string name) =>
        Window().Descendants().Single(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == name);

    private static XDocument Window() => XDocument.Load(LooseXaml.SourceFile("MainWindow.xaml"));
}
