using System.Collections.ObjectModel;
using Theseus.Core.Finding;

namespace Theseus.App.ViewModels;

/// <summary>
/// The board's search box and tag filter (card-search-and-tags, Nigel 2026-11-21): what is typed, what is ticked, whether
/// the rest is dimmed or hidden, and what the status bar and the title line say about it. The board applies it; this
/// holds the owner's choices and the readings the board gives back.
/// </summary>
public sealed class BoardSearchViewModel : ObservableObject
{
    private readonly Action _changed;
    private readonly SearchHistory _history = new();
    private string _text = string.Empty;
    private SearchQuery _query = SearchQuery.Empty;
    private bool _hideRest;
    private TagMatch _match = TagMatch.Any;
    private int _matching;
    private int _total;
    private bool _archivedWouldMatch;

    /// <summary>Creates the search; <paramref name="changed"/> runs whenever what it filters by changes.</summary>
    public BoardSearchViewModel(Action changed)
    {
        _changed = changed;
        TickedTags.CollectionChanged += (_, _) => Changed();
        ClearCommand = new RelayCommand(_ => Clear(), _ => CanClear);
    }

    /// <summary>What is typed in the box (item 2). It filters as it changes.</summary>
    public string Text
    {
        get => _text;
        set
        {
            if (Set(ref _text, value ?? string.Empty))
            {
                _query = SearchQuery.Parse(_text);
                Raise(nameof(ShowsPlaceholder));
                Changed();
            }
        }
    }

    /// <summary>Whether the box is empty, so it shows "Find cards (Ctrl+F)" (item 1).</summary>
    public bool ShowsPlaceholder => _text.Length == 0;

    /// <summary>The words and <c>#</c>tags typed.</summary>
    public SearchQuery Query => _query;

    /// <summary>The tags ticked in the Tags list (item 12).</summary>
    public ObservableCollection<string> TickedTags { get; } = [];

    /// <summary>Any or All, for the tags (item 12).</summary>
    public TagMatch Match
    {
        get => _match;
        set
        {
            if (Set(ref _match, value))
            {
                Raise(nameof(IsAll));
                Raise(nameof(IsAny));
                Changed();
            }
        }
    }

    /// <summary>The Any half of the Any | All toggle.</summary>
    public bool IsAny
    {
        get => _match == TagMatch.Any;
        set
        {
            if (value)
            {
                Match = TagMatch.Any;
            }
        }
    }

    /// <summary>The All half of the Any | All toggle.</summary>
    public bool IsAll
    {
        get => _match == TagMatch.All;
        set
        {
            if (value)
            {
                Match = TagMatch.All;
            }
        }
    }

    /// <summary>The search and the tags together, as the board applies them (item 15).</summary>
    public CardFilter Filter => new(_query, [.. TickedTags], _match);

    /// <summary>Whether anything filters the board.</summary>
    public bool IsOn => Filter.IsOn;

    /// <summary>Hide the rest (item 3): cards that do not match are hidden instead of dimmed. Off by default (decision 1).</summary>
    public bool HideRest
    {
        get => _hideRest;
        set
        {
            if (Set(ref _hideRest, value))
            {
                _changed();
            }
        }
    }

    /// <summary>The box's last few searches, most recent first (item 8).</summary>
    public ObservableCollection<string> History { get; } = [];

    /// <summary>Keeps what is typed in the drop-down: called on Enter and when the box is left.</summary>
    public void Remember()
    {
        if (_history.Add(_text))
        {
            History.Clear();

            foreach (var item in _history.Items)
            {
                History.Add(item);
            }
        }
    }

    /// <summary>How many cards on the board match, as the board last counted.</summary>
    public int Matching => _matching;

    /// <summary>How many cards the board holds, as the board last counted.</summary>
    public int Total => _total;

    /// <summary>Whether no card on the board matches but an archived one would (item 5).</summary>
    public bool ShowsIncludeArchived => IsOn && _matching == 0 && _archivedWouldMatch;

    /// <summary>
    /// What the status bar says while it filters (item 5): "7 of 112 cards match", or "No card matches 'xyz'"; empty when
    /// nothing filters, so the board's own count shows.
    /// </summary>
    public string StatusText => !IsOn ? string.Empty
        : _matching > 0 ? $"{_matching} of {_total} {(_total == 1 ? "card" : "cards")} {(_matching == 1 ? "matches" : "match")}"
        : $"No card matches {Describe()}";

    /// <summary>Whether the status bar shows <see cref="StatusText"/> in place of the board's count.</summary>
    public bool HasStatus => IsOn;

    /// <summary>The board's title line while it filters (item 17), so it never looks complete when it is not.</summary>
    public string FilteredLine => Filter.Describe(_matching, _total);

    /// <summary>Clear (item 16): empties the box and unticks every tag.</summary>
    public RelayCommand ClearCommand { get; }

    /// <summary>Whether Clear has anything to clear.</summary>
    public bool CanClear => _text.Length > 0 || TickedTags.Count > 0;

    /// <summary>What Clear says, enabled or not (1.1e item 6b: a button that cannot act says why).</summary>
    public string ClearToolTip => CanClear ? "Clear the search and the tag filter (Esc)" : "Nothing to clear: no search or tag is on";

    /// <summary>Empties the box and unticks every tag; the board then shows exactly as before (item 16).</summary>
    public void Clear()
    {
        if (!CanClear)
        {
            return;
        }

        _text = string.Empty;
        _query = SearchQuery.Empty;
        Raise(nameof(Text));
        Raise(nameof(ShowsPlaceholder));
        TickedTags.Clear();
        Changed();
    }

    /// <summary>The board's counts after applying the filter.</summary>
    internal void Counted(int matching, int total, bool archivedWouldMatch)
    {
        _matching = matching;
        _total = total;
        _archivedWouldMatch = archivedWouldMatch;
        RaiseReadings();
    }

    private string Describe()
    {
        var filter = Filter;
        var parts = filter.Tags.Select(tag => "#" + tag).Concat(filter.Query.Words);

        return $"'{string.Join(' ', parts)}'";
    }

    private void Changed()
    {
        Raise(nameof(IsOn));
        Raise(nameof(Filter));
        Raise(nameof(CanClear));
        Raise(nameof(ClearToolTip));
        _changed();
        RaiseReadings();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private void RaiseReadings()
    {
        Raise(nameof(Matching));
        Raise(nameof(Total));
        Raise(nameof(StatusText));
        Raise(nameof(HasStatus));
        Raise(nameof(FilteredLine));
        Raise(nameof(ShowsIncludeArchived));
    }
}
