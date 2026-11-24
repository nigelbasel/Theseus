using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Theseus.App.ViewModels;
using Theseus.Core.Instructions;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Layout the 1.9 baseline's re-run found after the fix batches (WB9; checklist G94, G103, G109, G110, G112, G113,
/// G115; decisions W36, W38, W56): sizes set outside Margin and Padding sit on the 4 epx grid, a hit area is at least
/// 8 for an edge and 24 for a target, controls in one row share one height, and text keeps the type ramp and a reading width.
/// </summary>
[TestFixture]
public class BaselineRerunLayoutTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void AnImportedInstructionFile_IsIndentedOnTheGrid() =>
        Assert.That(InstructionRow.For(new InstructionFile("a.md", InstructionScope.Project, InstructionLoad.AtStart, 1, "always")).Indent.Left % 4,
            Is.Zero, "ND-1, G109: set in C#, so the XAML ratchet cannot see it");

    [Test]
    public void TheBoardsRowEdge_HasAnEightEpxHitArea() =>
        Assert.That(Load("MainWindow.xaml").Descendants(Presentation + "Thumb").Where(thumb => (string?)thumb.Attribute("Cursor") == "SizeNS")
            .Select(thumb => (string?)thumb.Attribute("Height")), Is.All.EqualTo("8"), "ND-4, W38");

    [Test]
    public void TheSharedSelectorBarItem_Is32High_SoARowOfItAndButtonsIsOneHeight()
    {
        XElement style = Load("Themes/Interactive.xaml").Descendants(Presentation + "Style").Single(each => Key(each) == "SelectorBarItem");

        Assert.That(Setter(style, "MinHeight"), Is.EqualTo("32"), "ND-3, W56");
        Assert.That(Setter(style, "Padding"), Is.EqualTo("12,4"));
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void TheRecordsWindowsPreviewAndSource_AreAsHighAsItsButtons()
    {
        using OpenedWindow opened = OpenedWindow.Of("Records");

        Assert.That(opened.Named<FrameworkElement>("PreviewTab").ActualHeight, Is.EqualTo(32).Within(0.5), "ND-3, W56, G113");
    }

    [Test]
    public void TheTagsHeader_SitsEightAboveItsControl_AsTitleAndColourDo() =>
        Assert.That(Load("ProjectPropertiesWindow.xaml").Descendants(Presentation + "TextBlock").Single(text => (string?)text.Attribute("Text") == "Tags")
            .Attribute("Margin")?.Value, Is.EqualTo("0,20,0,8"), "ND-5, W36, G110");

    [TestCase("Title")]
    [TestCase("Colour")]
    [TestCase("Tags")]
    public void AProjectPropertiesHeader_IsBodyStrong(string header) =>
        Assert.That(Load("ProjectPropertiesWindow.xaml").Descendants(Presentation + "TextBlock").Single(text => (string?)text.Attribute("Text") == header)
            .Attribute("Style")?.Value, Is.EqualTo("{DynamicResource BodyStrongText}"), "A25, G94");

    [Test]
    public void ProjectPropertiesTagRemove_IsAGlyphButtonWithA32Target()
    {
        XElement remove = Load("ProjectPropertiesWindow.xaml").Descendants(Presentation + "Button")
            .Single(button => ((string?)button.Attribute("AutomationProperties.Name"))?.StartsWith("{Binding StringFormat=Remove tag", StringComparison.Ordinal) == true);

        Assert.That(((string?)remove.Attribute("Content"), (string?)remove.Attribute("Style")), Is.EqualTo(("\uE711", "{DynamicResource ToolButton}")),
            "D4, C2, W38, W24");
    }

    [Test]
    public void TagColourSwatches_AreAtLeast24Square()
    {
        Assert.That(Load("SettingsWindow.xaml").Descendants(Presentation + "Button").Where(button => (string?)button.Attribute("Width") == "24")
            .Select(button => (string?)button.Attribute("Height")), Is.All.EqualTo("24"), "D4, W38");
        Assert.That(Element("ProjectPropertiesWindow.xaml", "Swatch").Attribute("Height")!.Value, Is.EqualTo("24"), "D4, W38");
    }

    [Test]
    public void Options_HasAMinimumWidth_AndA24Gutter()
    {
        XElement window = Load("SettingsWindow.xaml").Root!;

        Assert.That((string?)window.Attribute("MinWidth"), Is.Not.Null, "D13, G115");
        Assert.That(Element("SettingsWindow.xaml", "Tabs").Attribute("Margin")!.Value, Is.EqualTo("20,12,20,0"), "D11: with the Page style's 4, 24 from the edge");
    }

    [Test]
    public void AFieldHint_WrapsWithinAReadingWidth()
    {
        XElement hint = Load("NewWorkstreamWindow.xaml").Descendants(Presentation + "Style").Single(each => Key(each) == "FieldHint");

        Assert.That((Setter(hint, "MaxWidth"), Setter(hint, "TextWrapping")), Is.EqualTo(("480", "Wrap")), "A29, G103");
    }

    [Test]
    public void AnInstructionRowsTitle_IsBodySize() =>
        Assert.That(Load("InstructionsWindow.xaml").Descendants(Presentation + "TextBlock").First(text => (string?)text.Attribute("Text") == "{Binding Title}")
            .Attribute("FontSize")?.Value, Is.EqualTo("14"), "D14, G94");

    [Test]
    public void ASessionRowsAdoptButton_IsAsHighAsItsFocusButton() =>
        Assert.That(Element("MainWindow.xaml", "AdoptButton").Attribute("MinHeight")?.Value, Is.EqualTo("32"),
            "ND-6, W56: the 24 chip is for a card's line of text, and this row is its own buttons");

    private static string? Setter(XElement style, string property) =>
        (string?)style.Elements(Presentation + "Setter").FirstOrDefault(setter => (string?)setter.Attribute("Property") == property)?.Attribute("Value");

    private static string? Key(XElement element) => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key");

    private static XElement Element(string file, string name) =>
        Load(file).Descendants().Single(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == name);

    private static XDocument Load(string file) => XDocument.Load(LooseXaml.SourceFile(file));
}
