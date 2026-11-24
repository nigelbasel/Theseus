using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A round's pieces in one state, under a heading that folds, worded as that state's count chip: "▶ ✓ 8 done". Each
/// state has its own, so built pieces not yet checked no longer follow the finished ones as if they were finished, and
/// no piece sits under the chain of the session that handed it work (git-22, 2026-11-24; G126, G135).
/// </summary>
public sealed class RoundStateGroupViewModel : ObservableObject, IFoldable
{
    private bool _isOpen;
    private bool _isShown = true;

    internal RoundStateGroupViewModel(int round, StageNodeState state, IReadOnlyList<PieceNodeViewModel> boxes, bool isOpen,
        Action<RoundStateGroupViewModel> toggle)
    {
        State = state;
        Boxes = boxes;
        FoldName = Entry(round, state);
        _isOpen = isOpen;
        ToggleCommand = new RelayCommand(_ => toggle(this));
    }

    /// <summary>The fold's entry for a round's state.</summary>
    public static string Entry(int round, StageNodeState state) => $"state:{round}:{state}";

    /// <summary>Finished work starts folded (D22); every other state starts open, as it may need him.</summary>
    public static bool OpensAtFirst(StageNodeState state) => state != StageNodeState.Done;

    /// <summary>The state its boxes are in.</summary>
    public StageNodeState State { get; }

    /// <summary>Its boxes, in the round's order.</summary>
    public IReadOnlyList<PieceNodeViewModel> Boxes { get; }

    /// <inheritdoc />
    public string FoldName { get; }

    /// <summary>"▶ ✓ 8 done" folded, "▼ ✓ 8 done" open: the chip's own words, counting what the filter shows.</summary>
    public string Text => $"{(_isOpen ? "▼" : "▶")} {StateCountViewModel.TextOf(State, Shown.Count)}";

    /// <summary>The titles of what the filter shows, in order, shown while it is folded.</summary>
    public string Titles => string.Join(" · ", Shown.Select(box => box.Title));

    private List<PieceNodeViewModel> Shown => [.. Boxes.Where(box => box.IsShown)];

    /// <summary>Follows the filter: shown while any box is, counting those; opened when asked, which is not remembered.</summary>
    internal void ShowFiltered(bool opens)
    {
        IsShown = Boxes.Any(box => box.IsShown);

        if (opens && IsShown)
        {
            IsOpen = true;
        }

        Raise(nameof(Text));
        Raise(nameof(Titles));
    }

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

    /// <inheritdoc />
    public bool StartsOpen => OpensAtFirst(State);

    /// <summary>Whether only its heading shows.</summary>
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
