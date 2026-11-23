using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Where Start the checks… and Verify with holdouts are drawn, a refusal is in plain view (Nigel, 2026-11-23): the
/// menus' own text names the reason, a box's disabled button has its reason beside it, and the Waits button keeps its
/// tooltip while disabled.
/// </summary>
[TestFixture]
public class CheckRefusalMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument Load(params string[] path) =>
        XDocument.Load(Path.Combine([RepositoryRoot(), "src", "Theseus.App", .. path]));

    private static XElement MenuItemFor(XDocument window, string command) =>
        window.Descendants(Presentation + "MenuItem").Single(item => (string?)item.Attribute("Command") == command);

    [Test]
    public void BothVerifyMenuItems_TakeTheirTextFromTheReason()
    {
        var window = Load("MainWindow.xaml");

        Assert.That((string?)MenuItemFor(window, "{Binding VerifyWithHoldoutsCommand}").Attribute("Header"),
            Is.EqualTo("{Binding VerifyMenuText}"));
        Assert.That((string?)MenuItemFor(window, "{Binding VerifySelectedWithHoldoutsCommand}").Attribute("Header"),
            Is.EqualTo("{Binding VerifySelectedMenuText}"));
    }

    [Test]
    public void AProjectViewMenuItem_ShowsItsMenuText()
    {
        var style = Load("Views", "ProjectView.xaml").Descendants(Presentation + "Style")
            .Single(each => (string?)each.Attribute(Xaml + "Key") == "StageMenuItem");
        var header = style.Elements(Presentation + "Setter").Single(setter => (string?)setter.Attribute("Property") == "Header");

        Assert.That((string?)header.Attribute("Value"), Is.EqualTo("{Binding MenuText}"));
    }

    [Test]
    public void ABoxsButton_DrawsItsNoteBesideIt()
    {
        var template = Load("Views", "ProjectView.xaml").Descendants(Presentation + "DataTemplate")
            .First(each => (string?)each.Attribute("DataType") == "{x:Type vm:PieceActionViewModel}");

        Assert.That(template.Descendants(Presentation + "TextBlock").Any(text => (string?)text.Attribute("Text") == "{Binding Note}"),
            Is.True);
    }

    [Test]
    public void TheWaitsButton_KeepsItsTooltipWhileDisabled()
    {
        var button = Load("Views", "ProjectView.xaml").Descendants(Presentation + "Button")
            .Single(each => (string?)each.Attribute(Xaml + "Name") == "WaitButton");

        Assert.That(((string?)button.Attribute("ToolTip"), (string?)button.Attribute("ToolTipService.ShowOnDisabled")),
            Is.EqualTo(("{Binding ToolTip}", "True")));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
