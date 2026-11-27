using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One line of "Waits for you", with the button that does it, or none for something outside Theseus (1.1f G36). The
/// view may say more than the model knows: when no independent check can run, the line says why and its button is the
/// way on (Nigel, 2026-11-23).
/// </summary>
public sealed class WaitItemViewModel : ObservableObject
{
    private bool _isSearchHit;

    private readonly string? _text;
    private readonly string? _button;

    /// <summary>Creates the line.</summary>
    /// <param name="item">The item.</param>
    /// <param name="command">What its button does, or null for none.</param>
    /// <param name="text">The line in place of the item's own, or null.</param>
    /// <param name="button">The button's label in place of the item's own, or null.</param>
    /// <param name="toolTip">What the button does, or why it cannot, or null.</param>
    internal WaitItemViewModel(WaitItem item, ICommand? command, string? text = null, string? button = null, string? toolTip = null)
    {
        Item = item;
        Command = item.Button is null ? null : command;
        _text = text;
        _button = button;
        ToolTip = toolTip;
    }

    /// <summary>The item.</summary>
    public WaitItem Item { get; }

    /// <summary>The line, in plain words.</summary>
    public string Text => _text ?? Item.Text;

    /// <summary>The button's label, or null.</summary>
    public string? Button => Command is null ? null : _button ?? Item.Button;

    /// <summary>Whether it has a button.</summary>
    public bool HasButton => Command is not null;

    /// <summary>What the button does, or why it cannot: shown on the disabled button too.</summary>
    public string? ToolTip { get; }

    /// <summary>What the button does.</summary>
    public ICommand? Command { get; }

    /// <summary>
    /// A second button before the first, or null: a spec's View spec beside its Accept spec… (board-small-changes item 4,
    /// Nigel, 2026-11-26: "there should be a view spec button").
    /// </summary>
    public string? SecondButton { get; init; }

    /// <summary>What the second button does.</summary>
    public ICommand? SecondCommand { get; init; }

    /// <summary>The second button's tooltip.</summary>
    public string? SecondToolTip { get; init; }

    /// <summary>Whether it has the second button.</summary>
    public bool HasSecondButton => SecondCommand is not null && SecondButton is not null;

    /// <summary>
    /// Whether the board's search found the card it concerns, which has no box to light, such as a scenario writer or a
    /// verifier (Nigel, 2026-11-26).
    /// </summary>
    public bool IsSearchHit
    {
        get => _isSearchHit;
        set => Set(ref _isSearchHit, value);
    }
}
