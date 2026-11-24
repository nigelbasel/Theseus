using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Theseus.App.Controls;

/// <summary>
/// A decorative glyph (baseline W50, finding E10): drawn as text, but left out of the control and content views of
/// the automation tree, so a screen reader reads the button's name and not a private-use character. Use it only where
/// something else names what the glyph means.
/// </summary>
public class GlyphText : TextBlock
{
    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlyphTextAutomationPeer(this);
}
