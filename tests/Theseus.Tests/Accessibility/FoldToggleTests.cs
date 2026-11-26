using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Interop;
using Theseus.App.Controls;

namespace Theseus.Tests.Accessibility;

/// <summary>
/// The locks panel's fold header (Nigel, 2026-11-26; 1.9's conventions): Right unfolds and Left folds, as in a tree, and a
/// screen reader hears it as expanded or collapsed, not only as pressed.
/// </summary>
[TestFixture]
public class FoldToggleTests
{
    [Test]
    [Apartment(ApartmentState.STA)]
    public void Right_Unfolds_AndLeft_Folds()
    {
        var fold = new FoldToggle { IsChecked = false };

        Assert.That(Press(fold, Key.Right), Is.True);
        Assert.That(fold.IsChecked, Is.True);

        Assert.That(Press(fold, Key.Left), Is.True);
        Assert.That(fold.IsChecked, Is.False);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void Right_OnAnOpenFold_IsLeftForTheNextControl()
    {
        var fold = new FoldToggle { IsChecked = true };

        Assert.That(Press(fold, Key.Right), Is.False);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void AScreenReader_HearsItExpandedOrCollapsed()
    {
        var fold = new FoldToggle { IsChecked = false };
        var peer = UIElementAutomationPeer.CreatePeerForElement(fold);
        var expander = (IExpandCollapseProvider)peer.GetPattern(PatternInterface.ExpandCollapse);

        Assert.That(expander.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Collapsed));

        expander.Expand();

        Assert.That(fold.IsChecked, Is.True);
        Assert.That(expander.ExpandCollapseState, Is.EqualTo(ExpandCollapseState.Expanded));

        expander.Collapse();

        Assert.That(fold.IsChecked, Is.False);
    }

    private static bool Press(FoldToggle fold, Key key)
    {
        var source = new HwndSource(new HwndSourceParameters("fold-toggle-test")) { RootVisual = fold };

        try
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
            fold.RaiseEvent(args);

            return args.Handled;
        }
        finally
        {
            source.RootVisual = null;
            source.Dispose();
        }
    }
}
