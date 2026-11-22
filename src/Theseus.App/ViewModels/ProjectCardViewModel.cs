using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// A project on the board, standing for its cards (project-view AC 12 and 13; 1.1d AC 38 to 44): unmistakably a project
/// by its stripe, mark and the word PROJECT in its own colour; where it is, in the project view's sentence and counts;
/// selected, dragged, started and stopped as a card is.
/// </summary>
public sealed class ProjectCardViewModel : ObservableObject, IBoardCard
{
    /// <summary>The colour of a project not given one yet: the To do grey.</summary>
    public const string NoColour = "#8A9490";

    private string _title;
    private string _phaseText = string.Empty;
    private string _objectivesText = string.Empty;
    private IReadOnlyList<string> _waiting = [];
    private bool _isDimmed;
    private string? _warning;
    private string? _problem;
    private string _colourHex = NoColour;
    private string _sentence = string.Empty;
    private IReadOnlyList<StateCountViewModel> _counts = [];
    private bool _isSelected;
    private bool _isLive;
    private ProjectStart? _startPlan;
    private IReadOnlyList<string>? _runState;
    private ProjectStop? _stopPlan;
    private IReadOnlyList<string> _tags = [];
    private bool _isSearchDimmed;
    private IReadOnlyList<Theseus.Core.Finding.TextSpan> _titleHighlights = [];
    private int _matchesInside;
    private IReadOnlyList<TagChipViewModel> _tagChips = [];
    private string _moreTags = string.Empty;
    private string _moreTagsToolTip = string.Empty;
    private IReadOnlyList<TagMenuItemViewModel> _tagMenu = [];

    /// <summary>Creates a project card.</summary>
    /// <param name="name">The project's name: its project home folder's name.</param>
    /// <param name="title">Its title.</param>
    public ProjectCardViewModel(string name, string title)
    {
        Name = name;
        _title = title;
    }

    /// <summary>The project's name, which keys its place on the board, its colour and its view.</summary>
    public string Name { get; }

    /// <summary>Its title.</summary>
    public string Title
    {
        get => _title;
        set
        {
            if (Set(ref _title, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>
    /// Whether its project.md does not read (pinned to an older kit, or out of shape): the card is dimmed, says why, and
    /// opens the file instead of the project view (1.1c AC 5). It keeps its mark and colour, and has no ▶ or ■ (1.1d AC 44).
    /// </summary>
    public bool IsDimmed
    {
        get => _isDimmed;
        set
        {
            if (Set(ref _isDimmed, value))
            {
                Raise(nameof(CardOpacity));
                Raise(nameof(OpenToolTip));
                Raise(nameof(OpenButtonToolTip));
                Raise(nameof(ShowsRunButtons));
            }
        }
    }

    /// <summary>The card's opacity: dimmed further while a search is on that it does not match, and when its project does not read.</summary>
    public double CardOpacity => _isSearchDimmed ? 0.3 : _isDimmed ? 0.55 : 1.0;

    /// <summary>The owner's tags on the project card, kept in settings by project name (card-search-and-tags item 9).</summary>
    public IReadOnlyList<string> Tags
    {
        get => _tags;
        set => Set(ref _tags, value);
    }

    /// <summary>Whether a search is on that neither the project nor any of its cards matches, so it is dimmed (item 3).</summary>
    public bool IsSearchDimmed
    {
        get => _isSearchDimmed;
        private set
        {
            if (Set(ref _isSearchDimmed, value))
            {
                Raise(nameof(CardOpacity));
            }
        }
    }

    /// <summary>Where the search's words fall in the title (item 3).</summary>
    public IReadOnlyList<Theseus.Core.Finding.TextSpan> TitleHighlights
    {
        get => _titleHighlights;
        private set => Set(ref _titleHighlights, value);
    }

    /// <summary>How many of its cards match the search (item 7).</summary>
    public int MatchesInside
    {
        get => _matchesInside;
        private set
        {
            if (Set(ref _matchesInside, value))
            {
                Raise(nameof(MatchesInsideText));
            }
        }
    }

    /// <summary>"1 match inside", under its title while a search matches its cards (item 7); empty otherwise.</summary>
    public string MatchesInsideText => _matchesInside == 0 ? string.Empty : $"{_matchesInside} match{(_matchesInside == 1 ? string.Empty : "es")} inside";

    /// <summary>The keys of its cards the search matches, which opening it carries into the project view (item 7).</summary>
    public IReadOnlyList<string> MatchingKeys { get; private set; } = [];

    /// <summary>The tags shown under the title, at most three (card-search-and-tags item 11).</summary>
    public IReadOnlyList<TagChipViewModel> TagChips
    {
        get => _tagChips;
        private set
        {
            if (Set(ref _tagChips, value))
            {
                Raise(nameof(HasTags));
            }
        }
    }

    /// <summary>Whether it shows any tags.</summary>
    public bool HasTags => _tagChips.Count > 0;

    /// <summary>"+2" for the tags not shown, or empty.</summary>
    public string MoreTags
    {
        get => _moreTags;
        private set => Set(ref _moreTags, value);
    }

    /// <summary>The tags not shown, for the "+2" label's tooltip.</summary>
    public string MoreTagsToolTip
    {
        get => _moreTagsToolTip;
        private set => Set(ref _moreTagsToolTip, value);
    }

    /// <summary>Its menu's Tags ›, built as the menu opens (item 10).</summary>
    public IReadOnlyList<TagMenuItemViewModel> TagMenu
    {
        get => _tagMenu;
        set => Set(ref _tagMenu, value);
    }

    /// <summary>Shows its tags on its face, unchanged chips left in place.</summary>
    internal void ShowTags(IReadOnlyList<TagChipViewModel> chips, string more, string rest)
    {
        if (!chips.Select(chip => (chip.Tag, chip.ColourHex)).SequenceEqual(_tagChips.Select(chip => (chip.Tag, chip.ColourHex))))
        {
            TagChips = chips;
        }

        MoreTags = more;
        MoreTagsToolTip = rest;
    }

    /// <summary>Shows what the board's search makes of the project card.</summary>
    internal void ShowSearch(bool dimmed, IReadOnlyList<Theseus.Core.Finding.TextSpan> highlights, IReadOnlyList<string> matchingKeys)
    {
        IsSearchDimmed = dimmed;
        MatchingKeys = matchingKeys;
        MatchesInside = matchingKeys.Count;

        if (!highlights.SequenceEqual(_titleHighlights))
        {
            TitleHighlights = highlights;
        }
    }

    /// <summary>What Open does, for its menu item.</summary>
    public string OpenToolTip => _isDimmed
        ? "Update its project.md to the method's current shape: a short session does it, then stops"
        : "Open the project view in the main area (also View › Open project)";

    /// <summary>↗'s tooltip (1.1e AC 5): a dimmed card's ↗ opens its project.md, as its double-click does.</summary>
    public string OpenButtonToolTip => _isDimmed
        ? "Update its project.md to the method's current shape: a short session does it, then stops (Enter)"
        : "Open the project (Enter)";

    /// <summary>Whether ▶ and ■ show: only for a project that reads (1.1d AC 44).</summary>
    public bool ShowsRunButtons => !_isDimmed;

    /// <summary>The project's own colour, as <c>#rrggbb</c> (1.1d AC 40): its stripe, its mark, its tab and its summary.</summary>
    public string ColourHex
    {
        get => _colourHex;
        set => Set(ref _colourHex, value);
    }

    /// <summary>Its current phase, such as "Phase 1: Evidence", or why it does not read.</summary>
    public string PhaseText
    {
        get => _phaseText;
        set => Set(ref _phaseText, value);
    }

    /// <summary>"n of m objectives done" (departure 3), or what to do with a dimmed card.</summary>
    public string ObjectivesText
    {
        get => _objectivesText;
        set => Set(ref _objectivesText, value);
    }

    /// <summary>Where the project is, the project view's sentence (1.1d AC 8, 39); empty for a dimmed card.</summary>
    public string Sentence
    {
        get => _sentence;
        set
        {
            if (Set(ref _sentence, value))
            {
                Raise(nameof(HasSentence));
            }
        }
    }

    /// <summary>Whether the sentence line shows.</summary>
    public bool HasSentence => _sentence.Length > 0;

    /// <summary>The project view's count chips with the zeros left out (1.1d AC 39).</summary>
    public IReadOnlyList<StateCountViewModel> Counts
    {
        get => _counts;
        set
        {
            if (!_counts.SequenceEqual(value))
            {
                _counts = value;
                Raise();
            }
        }
    }

    /// <summary>What waits on the owner, one line each: sessions waiting for input, reports, proposals, specs.</summary>
    public IReadOnlyList<string> Waiting
    {
        get => _waiting;
        set
        {
            if (Set(ref _waiting, value))
            {
                RaiseCount();
            }
        }
    }

    /// <summary>How many things wait on the owner.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>Whether anything waits on him.</summary>
    public bool HasWaiting => _waiting.Count > 0;

    /// <summary>Whether the count's badge shows: something waits.</summary>
    public bool HasBadge => HasWaiting;

    /// <summary>The badge: the count.</summary>
    public string CountText => WaitingCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The waiting list, for the count's tooltip.</summary>
    public string WaitingText => _waiting.Count == 0 ? "Nothing is waiting on you" : string.Join(Environment.NewLine, _waiting);

    private void RaiseCount()
    {
        Raise(nameof(WaitingCount));
        Raise(nameof(WaitingText));
        Raise(nameof(HasWaiting));
        Raise(nameof(HasBadge));
        Raise(nameof(CountText));
    }

    /// <summary>
    /// What is wrong with what its file says that the owner should know, such as a card it names that another project
    /// names too (1.1c AC 2), or null.
    /// </summary>
    public string? Warning
    {
        get => _warning;
        set
        {
            if (Set(ref _warning, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>The card's tooltip: its title, why its file does not read, and its warning, when it has them.</summary>
    public string ToolTipText => string.Join(Environment.NewLine, new[] { Title, _problem, _warning }.OfType<string>());

    /// <summary>
    /// Why its project.md does not read, for the session that updates it: in the tooltip and Project properties, never on
    /// its face (1.1e AC 18). Null for a project that reads.
    /// </summary>
    public string? Problem
    {
        get => _problem;
        set
        {
            if (Set(ref _problem, value))
            {
                Raise(nameof(ToolTipText));
            }
        }
    }

    /// <summary>Not a project, on the menu of a card whose file does not read (1.1e AC 20).</summary>
    public System.Windows.Input.ICommand? HideCommand { get; set; }

    /// <summary>Whether it is selected (1.1d AC 41): it then has the card's 2 px accent border.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (Set(ref _isSelected, value))
            {
                SelectionChanged?.Invoke();
            }
        }
    }

    /// <summary>Raised when it is selected or unselected, so the selection commands look again.</summary>
    public Action? SelectionChanged { get; set; }

    /// <summary>Whether any of its cards has a session open, so Select live chooses it.</summary>
    public bool IsLive
    {
        get => _isLive;
        set => Set(ref _isLive, value);
    }

    /// <summary>The session-state column it sits in while the columns show session state, from its cards (1.1e AC 4).</summary>
    public string StateColumn { get; set; } = Theseus.Core.Domain.BoardView.NotRunning;

    /// <summary>The key of its first card, whose row it sits in while the rows show folders or chains (1.1e AC 4); null with none.</summary>
    public string? FirstCardKey { get; set; }

    /// <summary>
    /// Its run state while it is stopped: the cards that had a session when it was stopped (1.1e AC 8); null while it is
    /// not stopped.
    /// </summary>
    public IReadOnlyList<string>? RunState
    {
        get => _runState;
        set
        {
            if (!Equals(_runState, value) && !(_runState is not null && value is not null && _runState.SequenceEqual(value)))
            {
                _runState = value;
                Raise();
                Raise(nameof(IsStopped));
            }
        }
    }

    /// <summary>Whether it is stopped, which its card shows as ⏸ Project stopped (1.1e AC 8).</summary>
    public bool IsStopped => _runState is not null;

    /// <summary>The keys of the cards its pieces of work name, so a selection acts on each card once (1.1e AC 11).</summary>
    public IReadOnlyList<string> MemberKeys { get; set; } = [];

    /// <summary>A drag of it carries its name, marked as a project's (1.1d AC 42).</summary>
    public string DragPayload => Services.BoardGesture.ProjectPrefix + Name;

    /// <summary>What its Start would start, or null for a dimmed card (1.1e AC 9).</summary>
    public ProjectStart? StartPlan
    {
        get => _startPlan;
        set
        {
            if (Set(ref _startPlan, value))
            {
                Raise(nameof(StartToolTip));
            }
        }
    }

    /// <summary>What its Stop would ask, or null for a dimmed card.</summary>
    public ProjectStop? StopPlan
    {
        get => _stopPlan;
        set
        {
            if (Set(ref _stopPlan, value))
            {
                Raise(nameof(StopToolTip));
            }
        }
    }

    /// <summary>What ▶ does, or why it cannot.</summary>
    public string StartToolTip => _startPlan?.ToolTip ?? "A project that does not read cannot be started";

    /// <summary>What ■ does, or why it cannot.</summary>
    public string StopToolTip => _stopPlan?.ToolTip ?? "A project that does not read cannot be stopped";

    /// <summary>The full path of its project.md, for Project properties.</summary>
    public string ProjectFile { get; set; } = string.Empty;

    /// <summary>Open project.md: the project's file in its editor (1.1c AC 8).</summary>
    public ICommand? OpenFileCommand { get; set; }

    /// <summary>Add objective…, at the end of the current phase (1.1c AC 8); none for a dimmed card.</summary>
    public ICommand? AddObjectiveCommand { get; set; }

    /// <summary>Opens the project view (AC 14), or a dimmed card's project.md.</summary>
    public ICommand? OpenCommand { get; set; }

    /// <summary>▶: starts its run state, or its first card when it was never stopped (1.1e AC 9).</summary>
    public ICommand? StartCommand { get; set; }

    /// <summary>The lead card's key, or null when the project has no rows.</summary>
    public string? LeadKey
    {
        get => _leadKey;
        set => Set(ref _leadKey, value);
    }

    /// <summary>
    /// ⌖ Focus lead: brings the lead's terminal forward, disabled with its reason when it has no session. Raised when it
    /// changes, since a reused card is refilled on every refresh (review of PR 4, finding 2).
    /// </summary>
    public ICommand? FocusLeadCommand
    {
        get => _focusLead;
        set => Set(ref _focusLead, value);
    }

    /// <summary>"Bring &lt;lead&gt;'s terminal forward", or why it cannot.</summary>
    public string FocusLeadToolTip
    {
        get => _focusLeadToolTip;
        set => Set(ref _focusLeadToolTip, value);
    }

    private string? _leadKey;
    private ICommand? _focusLead;
    private string _focusLeadToolTip = string.Empty;

    /// <summary>■: records its run state and asks each of its running sessions to write its handover and stop (1.1e AC 8).</summary>
    public ICommand? StopCommand { get; set; }

    /// <summary>Project properties…: its title, colour and project.md (1.1d AC 40).</summary>
    public ICommand? PropertiesCommand { get; set; }
}
