using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// Two or more finished pieces one session worked through in turn, folded to one line "▶ n finished" and their titles
/// (1.1f D22, D12). The toggle opens them; they are the boxes in <see cref="Boxes"/>.
/// </summary>
public sealed class FinishedRunViewModel : ObservableObject, IFoldable
{
    private bool _isOpen;
    private bool _isShown = true;

    internal FinishedRunViewModel(IReadOnlyList<PieceNodeViewModel> boxes, bool isOpen, Action<FinishedRunViewModel> toggle)
    {
        Boxes = boxes;
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
    }

    /// <summary>The finished boxes, in file order.</summary>
    public IReadOnlyList<PieceNodeViewModel> Boxes { get; }

    /// <summary>Its fold entry: the first box's name, so it keeps its fold while the run grows.</summary>
    public string FoldName => $"run:{Boxes[0].Name}";

    /// <summary>"▶ 8 finished" folded, "▼ 8 finished" open.</summary>
    public string Text => $"{(_isOpen ? "▼" : "▶")} {Boxes.Count} finished";

    /// <summary>Their titles, in order.</summary>
    public string Titles => string.Join(" · ", Boxes.Select(box => box.Title));

    /// <summary>Whether its boxes are shown.</summary>
    public bool IsOpen
    {
        get => _isOpen;
        internal set
        {
            if (Set(ref _isOpen, value))
            {
                Raise(nameof(Text));
                Raise(nameof(IsFolded));
            }
        }
    }

    bool IFoldable.IsOpen
    {
        get => IsOpen;
        set => IsOpen = value;
    }

    /// <summary>A run starts folded (D22).</summary>
    public bool StartsOpen => false;

    /// <summary>Whether only its line shows.</summary>
    public bool IsFolded => !_isOpen;

    /// <summary>Whether the filter leaves any of its boxes shown.</summary>
    public bool IsShown
    {
        get => _isShown;
        internal set => Set(ref _isShown, value);
    }

    /// <summary>Opens or folds it.</summary>
    public ICommand ToggleCommand { get; }
}
