using System.Xml.Linq;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// Every resize edge says which thing it resizes, and every progress bar says what is in progress (baseline re-run NE-2;
/// checklist G92, G167): "Resize" alone, four times over, does not tell a screen reader which pane it is on.
/// </summary>
[TestFixture]
public class EdgeAndProgressNamesTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Controls = "clr-namespace:Theseus.App.Controls";

    [Test]
    public void EveryPaneEdge_IsNamedForItsPane_AndNoTwoAlike()
    {
        List<string?> names = [.. Load("MainWindow.xaml").Descendants(Controls + "PaneResizeThumb").Select(Name)];

        Assert.That(names, Has.Count.EqualTo(4));
        Assert.That(names, Is.All.Not.Null);
        Assert.That(names, Is.Unique);
    }

    [Test]
    public void EveryBoardEdge_IsNamed() =>
        Assert.That(Load("MainWindow.xaml").Descendants(Presentation + "Thumb").Select(Name), Is.All.Not.Null);

    [TestCase("InstructionsWindow.xaml")]
    [TestCase("MainWindow.xaml")]
    public void EveryProgressBar_IsNamed(string file) =>
        Assert.That(Load(file).Descendants(Presentation + "ProgressBar").Select(Name), Is.All.Not.Null);

    private static string? Name(XElement element) => (string?)element.Attribute("AutomationProperties.Name");

    private static XDocument Load(string file) => XDocument.Load(LooseXaml.SourceFile(file));
}
