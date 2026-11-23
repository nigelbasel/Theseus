using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// The fold of the boxes indented beneath a box in the rounds and work lens: the pieces it handed work to fold and
/// unfold together, with a chevron and a remembered state, as phases and objectives do (project-view-usability item 1,
/// Nigel 2026-11-23). Open at first, so nothing is hidden until he folds it; Collapse all and Expand all include it.
/// </summary>
public sealed class ChildrenFold : ObservableObject, IFoldable
{
    private readonly IReadOnlyList<string> _titles;
    private bool _isOpen;

    internal ChildrenFold(string piece, IReadOnlyList<string> titles, bool isOpen, Action<IFoldable> toggle)
    {
        FoldName = Entry(piece);
        _titles = titles;
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
    }

    /// <summary>The fold's entry for a piece's children.</summary>
    public static string Entry(string piece) => $"children:{piece}";

    /// <inheritdoc />
    public string FoldName { get; }

    /// <inheritdoc />
    public bool StartsOpen => true;

    /// <summary>Whether the boxes beneath are shown.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(IsFolded));
                Raise(nameof(Chevron));
                Raise(nameof(ToolTip));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <summary>Whether they are folded away, when the one line below shows in their place.</summary>
    public bool IsFolded => !_isOpen;

    /// <summary>▸ folded, ▾ open.</summary>
    public string Chevron => _isOpen ? "▾" : "▸";

    /// <summary>What the chevron does.</summary>
    public string ToolTip => _isOpen ? "Fold the pieces of work beneath this one" : "Show the pieces of work beneath this one";

    /// <summary>The one line shown while folded: how many, and their names.</summary>
    public string Summary => _titles.Count == 1
        ? $"1 piece of work beneath: {_titles[0]}"
        : $"{_titles.Count} pieces of work beneath: {string.Join(", ", _titles)}";

    /// <summary>Folds or unfolds them, remembered.</summary>
    public ICommand ToggleCommand { get; }
}
