using System.Windows;
using System.Windows.Automation.Peers;
using Theseus.Tests.Windows;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// A selector bar's name reaches a screen reader (baseline re-run NE-8; checklist G168, G169): a panel has no automation
/// peer, so a name set on one is never read; the group is a Group in the tree, named for what it switches.
/// </summary>
[TestFixture]
public class SelectorGroupTests
{
    [TestCase("Records", "Views of the document")]
    [TestCase("Spec", "Views of the spec")]
    [Apartment(ApartmentState.STA)]
    public void ADialogsSelectorBar_IsANamedGroup(string window, string name)
    {
        using OpenedWindow opened = OpenedWindow.Of(window);

        Assert.That(Groups(opened.Window), Does.Contain(name));
    }

    [Test]
    [NonParallelizable]
    public void TheBoardsSelectorBars_AreNamedGroups() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                shell.Chains.IsOpen = true;
                window.DataContext = null;
                window.DataContext = shell;
                FrameworkElement content = (FrameworkElement)window.Content;
                content.Measure(new Size(1400, 760));
                content.Arrange(new Rect(0, 0, 1400, 760));
                content.UpdateLayout();
                List<string> groups = Groups(content);

                Assert.That(groups, Does.Contain("Show"));
                Assert.That(groups, Does.Contain("Views of the side panel"));
            },
            board => board.With("alpha"));

    private static List<string> Groups(UIElement root) =>
        [.. AutomationTree.Walk(root).Where(peer => peer.Type == AutomationControlType.Group).Select(peer => peer.Name)];
}
