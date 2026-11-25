using System.Windows.Automation.Peers;

namespace Theseus.App.Controls;

/// <summary>A selector bar's panel to UI Automation: a Group, named by its AutomationProperties.Name.</summary>
/// <param name="owner">The panel.</param>
public sealed class SelectorGroupAutomationPeer(SelectorGroup owner) : FrameworkElementAutomationPeer(owner)
{
    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(SelectorGroup);
}
