using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Theseus.App.Controls;
using Theseus.App.ViewModels;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Keyboard gaps the 1.9 baseline's re-run found after the fix batches (WB9; checklist G64, G67, G78): every control is
/// reached from the keyboard, a selector group's Tab lands on its chosen item without changing it, and an accelerator
/// is shown where the command is.
/// </summary>
[TestFixture]
public class BaselineRerunKeyboardTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [TestCase(0, 3, "Ctrl+1")]
    [TestCase(7, 12, "Ctrl+8")]
    [TestCase(11, 12, "Ctrl+9")]
    [TestCase(9, 12, null)]
    public void AProjectTab_NamesTheKeyThatOpensIt(int index, int count, string? key) =>
        Assert.That(ProjectTabKeys.KeyFor(index, count), Is.EqualTo(key), "NB-4, G78");

    [Test]
    public void AClosedTabsTooltip_SaysItsKey() =>
        Assert.That(new ProjectTabViewModel("b", "Build", false, null) { Key = "Ctrl+2" }.ToolTip, Is.EqualTo("Open Build (Ctrl+2)"), "NB-4, G78");

    [Test]
    public void TheShowGroup_IsNotOneTabStop_SoTabReachesActiveOnly() =>
        Assert.That(Element("MainWindow.xaml", "BoardShowsChoice").Attribute("KeyboardNavigation.TabNavigation")?.Value, Is.Not.EqualTo("Once"),
            "NB-2, G64: the chosen item is the group's stop (SelectorBarItem), and Active only is a stop of its own");

    [TestCase("TagsAny")]
    [TestCase("TagsAll")]
    public void ATagMatchRadio_IsATabStopOnlyWhileChosen(string radio) =>
        Assert.That(Element("MainWindow.xaml", radio).Attribute("IsTabStop")?.Value, Is.EqualTo("{Binding IsChecked, RelativeSource={RelativeSource Self}}"),
            "NB-6, G67: Tab lands on the chosen one and does not change it");

    [Test]
    [Apartment(ApartmentState.STA)]
    public void APaneEdge_ResizesItsPaneFromTheKeyboard()
    {
        FrameworkElement pane = new() { Width = 300, MinWidth = 200, MaxWidth = 500 };
        PaneResizeThumb left = new() { Pane = pane };
        PaneResizeThumb right = new() { Pane = pane, IsOnTheRight = true };

        Assert.That(left.Focusable, Is.True, "NB-1, G64");
        Assert.That(left.Step(Key.Right), Is.True);
        Assert.That(pane.Width, Is.EqualTo(316), "a left-hand pane grows as its right edge moves right");
        Assert.That(right.Step(Key.Right), Is.True);
        Assert.That(pane.Width, Is.EqualTo(300), "a right-hand pane shrinks as its left edge moves right");
        Assert.That(left.Step(Key.Up), Is.False);
    }

    [Test]
    public void TheDrawer_TakesFocusWhenItOpens() =>
        Assert.That(Element("MainWindow.xaml", "DrawerPane").Attribute("IsVisibleChanged")?.Value, Is.EqualTo("OnDrawerShown"),
            "B3, W43: the first control of the drawer when it opens");

    [Test]
    public void TheTagsFlyout_TakesFocusWhenItOpens() =>
        Assert.That(Element("MainWindow.xaml", "TagsPopup").Attribute("Opened")?.Value, Is.EqualTo("OnTagsPopupOpened"),
            "NB-5, G66: focus moves to the chosen Any or All");

    private static XElement Element(string file, string name) =>
        XDocument.Load(LooseXaml.SourceFile(file)).Descendants()
            .Single(element => (string?)element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Name") == name);
}
