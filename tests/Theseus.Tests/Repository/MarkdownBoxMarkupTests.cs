using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace Theseus.Tests.Repository;

/// <summary>
/// Which boxes are Markdown boxes (1.1e AC 22, 24): the prompt boxes and Add objective's criteria are; Settings' stage and
/// area lists and the day report stay plain.
/// </summary>
[TestFixture]
public class MarkdownBoxMarkupTests
{
    private static readonly XNamespace Controls = "clr-namespace:Theseus.App.Controls";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestCase("NewWorkstreamWindow.xaml", "{Binding NewWorkstream.Prompt}")]
    [TestCase("SetupWindow.xaml", "{Binding Drawer.Prompt}")]
    [TestCase("SetupWindow.xaml", "{Binding Drawer.EveryStartPrompt}")]
    public void APromptBox_IsAMarkdownBox(string window, string binding)
    {
        Assert.That(Load(window).Descendants(Controls + "MarkdownBox").Select(box => (string?)box.Attribute("Text")), Does.Contain(binding));
    }

    [Test]
    public void OptionsPrompts_AreEditedInAMarkdownBox_InADialogOfTheirOwn()
    {
        Assert.That(Load("PromptEditWindow.xaml").Descendants(Controls + "MarkdownBox")
            .Any(box => (string?)box.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") == "Editor"), Is.True);
        Assert.That(Load("SettingsWindow.xaml").Descendants(Controls + "MarkdownBox"), Is.Empty, "Options itself holds no large text box (options-made-clear item 6)");
    }

    [Test]
    public void AddObjectivesCriteria_IsAMarkdownBox()
    {
        Assert.That(Load("AddObjectiveWindow.xaml").Descendants(Controls + "MarkdownBox")
            .Any(box => (string?)box.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") == "CriteriaBox"), Is.True);
    }

    [Test]
    public void TheStageAndAreaLists_StayPlain_InTheTextDialog()
    {
        Assert.That(Load("TextPromptWindow.xaml").Descendants(Presentation + "TextBox")
            .Any(box => (string?)box.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") == "Answer"), Is.True);
        Assert.That(Load("TextPromptWindow.xaml").Descendants(Controls + "MarkdownBox"), Is.Empty);
    }

    [Test]
    public void TheDayReport_StaysPlain()
    {
        Assert.That(Load("DayReportWindow.xaml").Descendants(Controls + "MarkdownBox"), Is.Empty);
    }

    private static XDocument Load(string window) =>
        XDocument.Load(Path.Combine(RepositoryRoot(), "src", "Theseus.App", window));

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
