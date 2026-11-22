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
    [TestCase("SettingsWindow.xaml", "{Binding IdentityPrompt}")]
    [TestCase("SettingsWindow.xaml", "{Binding SessionPreamble}")]
    [TestCase("SettingsWindow.xaml", "{Binding ContinuePrompt}")]
    [TestCase("SettingsWindow.xaml", "{Binding StopPrompt}")]
    public void APromptBox_IsAMarkdownBox(string window, string binding)
    {
        Assert.That(Load(window).Descendants(Controls + "MarkdownBox").Select(box => (string?)box.Attribute("Text")), Does.Contain(binding));
    }

    [Test]
    public void AddObjectivesCriteria_IsAMarkdownBox()
    {
        Assert.That(Load("AddObjectiveWindow.xaml").Descendants(Controls + "MarkdownBox")
            .Any(box => (string?)box.Attribute(XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml") + "Name") == "CriteriaBox"), Is.True);
    }

    [TestCase("SettingsWindow.xaml", "{Binding LaneList, UpdateSourceTrigger=PropertyChanged}")]
    [TestCase("SettingsWindow.xaml", "{Binding AreaList, UpdateSourceTrigger=PropertyChanged}")]
    public void TheStageAndAreaLists_StayPlain(string window, string binding)
    {
        Assert.That(Load(window).Descendants(Presentation + "TextBox").Select(box => (string?)box.Attribute("Text")), Does.Contain(binding));
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
