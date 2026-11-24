using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Where Start the checks… and Independent check are drawn, a refusal is in plain view (Nigel, 2026-11-23): the
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

    [TestCase("{Binding VerifyWithHoldoutsCommand}", "{Binding VerifyOffReason}")]
    [TestCase("{Binding VerifySelectedWithHoldoutsCommand}", "{Binding VerifySelectedOffReason}")]
    public void BothVerifyMenuItems_KeepAFixedLabel_WithTheReasonOnASecondLine(string command, string reason)
    {
        // The label never changes with the state (W30, G62); the reason is still in plain view, under it (Nigel, 2026-11-23).
        var item = MenuItemFor(Load("MainWindow.xaml"), command);

        Assert.That(item.Attribute("Header"), Is.Null, "no label bound to the state");
        Assert.That(item.Descendants(Presentation + "AccessText").Select(text => (string?)text.Attribute("Text")),
            Is.EqualTo(new[] { "Independent c_heck…" }));
        Assert.That(item.Descendants(Presentation + "TextBlock").Select(text => (string?)text.Attribute("Text")),
            Does.Contain(reason));
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
    public void ABoxsButton_HasItsNoteInPlainView_UnderTheRow()
    {
        // The actions are icons (walkthrough rows 24, 32), so a disabled one's reason is listed under the row, not beside it.
        var notes = Load("Views", "ProjectView.xaml").Descendants(Presentation + "ItemsControl")
            .First(each => (string?)each.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "ActionNotes");

        Assert.That(notes.Descendants(Presentation + "Run").Any(run => (string?)run.Attribute("Text") == "{Binding Note, Mode=OneWay}"),
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
