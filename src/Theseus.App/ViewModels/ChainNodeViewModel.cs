using System.Collections.ObjectModel;
using System.ComponentModel;
using Theseus.Core.Finding;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>
/// One card in the Chains tree, under the card that handed over to it. What it waits on and whom it
/// reports back to are lines beneath it, never lines drawn between rows.
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="ColorHex">Its colour, as a left border on the row.</param>
/// <param name="State">What its inputs say: "Ready", "waiting", or nothing.</param>
/// <param name="IsReady">Whether it is ready, shown in green.</param>
/// <param name="WaitsOn">What it waits on, with each input's state, or nothing.</param>
/// <param name="ReportsBack">The card it reports back to, with the report's state, or nothing.</param>
/// <param name="Show">Opens the card's details.</param>
public sealed record ChainNodeViewModel(
    string Key,
    string Title,
    string ColorHex,
    string State,
    bool IsReady,
    string WaitsOn,
    string ReportsBack,
    RelayCommand Show) : INotifyPropertyChanged
{
    private string _session = string.Empty;
    private RelayCommand? _focus;
    private bool _isExpanded = true;
    private ChainRowState _rowState = ChainRowState.Stopped;
    private bool _isShown = true;
    private bool _isContext;
    private IReadOnlyList<TextSpan> _titleHighlights = [];

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The cards it hands over to.</summary>
    public ObservableCollection<ChainNodeViewModel> Children { get; } = [];

    /// <summary>Whether its row is unfolded, showing the cards it hands over to; bound both ways to its tree item (walkthrough row 11).</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    /// <summary>
    /// "● running", "○ idle" or "◆ waiting" while it has a session; empty otherwise (item 8). Changed in place, so a
    /// session's every turn does not rebuild the tree and lose its selection and folds.
    /// </summary>
    public string Session
    {
        get => _session;
        set
        {
            if (_session != value)
            {
                _session = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Session)));
            }
        }
    }

    /// <summary>⌖ Focus: brings its tab forward in one click, while it has a session to focus; null otherwise (item 8).</summary>
    public RelayCommand? Focus
    {
        get => _focus;
        set
        {
            if (!ReferenceEquals(_focus, value))
            {
                _focus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Focus)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFocus)));
            }
        }
    }

    /// <summary>Whether ⌖ Focus shows on the row.</summary>
    public bool HasFocus => _focus is not null;

    /// <summary>Its key, shown when another row has the same title so the two can be told apart; empty otherwise.</summary>
    public string Copy { get; init; } = string.Empty;

    /// <summary>
    /// Whether it is a project's header rather than a card: shown only when the chains span two or more projects, with
    /// no state and no Focus (chains-panel-at-scale criterion 18, Nigel 2026-11-25).
    /// </summary>
    public bool IsGroup { get; init; }

    /// <summary>Its state, in one mark and word (chains-panel-at-scale criterion 12). Changed in place, as the session is.</summary>
    public ChainRowState RowState
    {
        get => _rowState;
        set
        {
            if (_rowState != value)
            {
                _rowState = value;
                Notify(nameof(RowState), nameof(StateText), nameof(ScreenName), nameof(NeedsOwner), nameof(IsMuted));
            }
        }
    }

    /// <summary>"◆ asking you", "● running", "✓ done" and so on; empty on a project's header.</summary>
    public string StateText => IsGroup ? string.Empty : $"{ChainRowStates.Mark(_rowState)} {ChainRowStates.Word(_rowState)}";

    /// <summary>What a screen reader names the row: its title and its state's word.</summary>
    public string ScreenName => IsGroup ? Title : $"{Title}, {ChainRowStates.Word(_rowState)}";

    /// <summary>Whether its state waits on the owner, drawn in the caution colour.</summary>
    public bool NeedsOwner => !IsGroup && _rowState is ChainRowState.AskingYou or ChainRowState.Reported
        or ChainRowState.ReportLost or ChainRowState.StartAsked;

    /// <summary>Whether it is finished with, its row muted.</summary>
    public bool IsMuted => !IsGroup && _rowState is ChainRowState.Done or ChainRowState.Archived;

    /// <summary>Whether the panel's search and filters keep it on screen, as a match or as context.</summary>
    public bool IsShown
    {
        get => _isShown;
        set
        {
            if (_isShown != value)
            {
                _isShown = value;
                Notify(nameof(IsShown));
            }
        }
    }

    /// <summary>Whether it is on screen only because a row below it is kept, drawn quieter.</summary>
    public bool IsContext
    {
        get => _isContext;
        set
        {
            if (_isContext != value)
            {
                _isContext = value;
                Notify(nameof(IsContext));
            }
        }
    }

    /// <summary>Where the panel's search words fall in the title, to be highlighted; empty with no search.</summary>
    public IReadOnlyList<TextSpan> TitleHighlights
    {
        get => _titleHighlights;
        set
        {
            if (!_titleHighlights.SequenceEqual(value))
            {
                _titleHighlights = value;
                Notify(nameof(TitleHighlights));
            }
        }
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
