using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Theseus.App.Controls;

/// <summary>
/// The panel of a selector bar's items (W15, W42): a Group to UI Automation, so the name it is given reaches a screen
/// reader (baseline re-run NE-8; G168, G169). A plain panel has no automation peer, and a name set on one is never read.
/// </summary>
public class SelectorGroup : StackPanel
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SelectorGroupAutomationPeer(this);
}
