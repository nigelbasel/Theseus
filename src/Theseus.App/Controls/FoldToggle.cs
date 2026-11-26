using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Theseus.App.Controls;

/// <summary>
/// A fold's header line on the locks panel, checked when open (Nigel, 2026-11-26). Right unfolds and Left folds, as in a
/// tree, and a screen reader hears expanded or collapsed (1.9's conventions), as the Chains panel's rows are heard.
/// </summary>
public class FoldToggle : ToggleButton
{
    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Key == Key.Right && IsChecked != true)
        {
            IsChecked = true;
            e.Handled = true;
        }
        else if (e.Key == Key.Left && IsChecked == true)
        {
            IsChecked = false;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnChecked(System.Windows.RoutedEventArgs e)
    {
        base.OnChecked(e);
        (UIElementAutomationPeer.FromElement(this) as FoldToggleAutomationPeer)?.RaiseState(false, true);
    }

    /// <inheritdoc />
    protected override void OnUnchecked(System.Windows.RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        (UIElementAutomationPeer.FromElement(this) as FoldToggleAutomationPeer)?.RaiseState(true, false);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new FoldToggleAutomationPeer(this);
}
