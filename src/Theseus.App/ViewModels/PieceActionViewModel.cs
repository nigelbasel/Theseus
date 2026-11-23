using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// One button or menu item of the project view (1.1d AC 27 to 29): its label, what it runs, and, when it is offered but
/// cannot run, why not as its tooltip.
/// </summary>
public sealed class PieceActionViewModel
{
    /// <summary>Creates an action.</summary>
    /// <param name="label">What it says.</param>
    /// <param name="run">What it runs.</param>
    /// <param name="isEnabled">Whether it can run now.</param>
    /// <param name="toolTip">What it does, or why it cannot.</param>
    /// <param name="isDefault">Whether it is its menu's default item, shown in bold.</param>
    public PieceActionViewModel(string label, Action run, bool isEnabled = true, string? toolTip = null, bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(run);

        Label = label;
        IsEnabled = isEnabled;
        ToolTip = toolTip;
        IsDefault = isDefault;
        Command = new RelayCommand(_ => run(), _ => isEnabled);
    }

    /// <summary>What it says.</summary>
    public string Label { get; }

    /// <summary>Whether it can run now.</summary>
    public bool IsEnabled { get; }

    /// <summary>What it does, or why it cannot; shown on the disabled button too.</summary>
    public string? ToolTip { get; }

    /// <summary>Whether it is the menu's default item (Properties), in bold.</summary>
    public bool IsDefault { get; }

    /// <summary>Runs it, when enabled.</summary>
    public ICommand Command { get; }

    /// <summary>
    /// Whether it is drawn as a link: the way on offered beside a disabled button, such as Set the holdout folder… (1.1e
    /// AC 6a).
    /// </summary>
    public bool IsLink { get; init; }

    /// <summary>
    /// Why it cannot run, drawn beside the disabled button, not only as its tooltip: Independent check's reason
    /// (Nigel, 2026-11-23: a refusal seen nowhere but the status line read as "nothing happens"). Null otherwise.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>Its text in a menu: the label, with the note after it when it has one, since a menu item shows no text beside it.</summary>
    public string MenuText => Note is null ? Label : $"{Label} — {Note}";
}
