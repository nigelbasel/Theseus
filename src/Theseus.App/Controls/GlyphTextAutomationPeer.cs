using System.Windows.Automation.Peers;

namespace Theseus.App.Controls;

/// <summary>A <see cref="GlyphText"/>'s peer: present in the raw view only.</summary>
public sealed class GlyphTextAutomationPeer(GlyphText owner) : TextBlockAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override bool IsControlElementCore() => false;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => false;
}
