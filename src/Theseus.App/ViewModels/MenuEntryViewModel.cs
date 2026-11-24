using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// One item of a project-view context menu: an action with the access key its menu gave it. The key lives here, not in
/// the action's label, so a button showing the same action shows no underscore (baseline W26, finding C9). A disabled
/// action's note is a second, dimmer line, so the header never changes with the state (W30, C10) and the reason stays in
/// plain view (Nigel, 2026-11-23).
/// </summary>
/// <param name="Header">The label with its access key marked, any underscore of its own doubled.</param>
/// <param name="Action">The action it runs.</param>
public sealed record MenuEntryViewModel(string Header, PieceActionViewModel Action)
{
    /// <summary>Runs the action, when enabled.</summary>
    public ICommand Command => Action.Command;

    /// <summary>What it does, or why it cannot.</summary>
    public string? ToolTip => Action.ToolTip;

    /// <summary>Why it cannot run, on the item's second line; null when it can.</summary>
    public string? Note => Action.Note;

    /// <summary>Whether it is the menu's default item, in bold.</summary>
    public bool IsDefault => Action.IsDefault;

    /// <summary>What UI Automation reads: the label, without the marks a text button carried.</summary>
    public string SpokenLabel => Action.SpokenLabel;
}
