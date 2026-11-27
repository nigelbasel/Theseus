using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;

namespace Theseus.App.Controls;

/// <summary>A <see cref="FoldToggle"/>'s peer: expand and collapse, so a screen reader says whether the fold is open.</summary>
public sealed class FoldToggleAutomationPeer(FoldToggle owner) : ToggleButtonAutomationPeer(owner), IExpandCollapseProvider
{
    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState =>
        ((FoldToggle)Owner).IsChecked == true ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public void Expand() => ((FoldToggle)Owner).IsChecked = true;

    /// <inheritdoc />
    public void Collapse() => ((FoldToggle)Owner).IsChecked = false;

    /// <inheritdoc />
    public override object GetPattern(PatternInterface patternInterface) =>
        patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

    internal void RaiseState(bool wasOpen, bool isOpen) =>
        RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            wasOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            isOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);
}
