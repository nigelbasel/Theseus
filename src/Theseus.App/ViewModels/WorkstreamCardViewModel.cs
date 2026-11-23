using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>One card on the board.</summary>
public sealed class WorkstreamCardViewModel : ObservableObject, IBoardCard
{
    private Workstream _workstream;
    private SessionLifecycle _lifecycle = SessionLifecycle.NotStarted;
    private Theseus.Core.Questions.OwnerQuestion? _question;
    private Theseus.Core.Projects.WhyStopped? _stopReason;
    private string _verifyToolTip = string.Empty;
    private DateTimeOffset? _branchRulesSince;
    private DateTimeOffset? _lastExitedUtc;
    private DateTimeOffset? _lastStartedUtc;
    private string _todoSummary = string.Empty;
    private string _resources = string.Empty;
    private string _resourcesTooltip = string.Empty;
    private bool _stopPending;
    private bool _isStopping;
    private string _stopTooltip = string.Empty;
    private bool _canCloseNow;
    private string _handoffRenameTo = string.Empty;
    private string _runningAs = string.Empty;
    private string _clash = string.Empty;
    private string _linkedShare = string.Empty;
    private LinkLineText _links = LinkLineText.None;
    private bool _isLinkHighlighted;
    private string _restartCost = string.Empty;
    private string _resumeTooltip = string.Empty;
    private RelayCommand? _resumeCommand;
    private RelayCommand? _startFreshCommand;
    private RelayCommand? _toggleBlockedCommand;
    private RelayCommand? _toggleReopenTomorrowCommand;
    private RelayCommand? _spawnFollowOnCommand;
    private string _model = string.Empty;
    private bool _motionAllowed = true;
    private string _lostSessionName = string.Empty;
    private bool _isSelected;
    private bool _isAdoptTarget;
    private bool _isCollapsed;
    private IReadOnlyList<PullRequestLink> _pullRequests = [];
    private IReadOnlyList<CardItemRow> _jiraItems = [];
    private string _projectSource = string.Empty;
    private IReadOnlyList<CardItemRow> _pullRequestItems = [];
    private string _branch = string.Empty;
    private string _branchTooltip = string.Empty;
    private bool _branchesDiffer;
    private DateTimeOffset? _lifecycleSinceUtc;
    private IReadOnlyList<CardRepo> _repos = [];
    private bool _isSearchDimmed;
    private IReadOnlyList<Theseus.Core.Finding.TextSpan> _titleHighlights = [];
    private IReadOnlyList<TagChipViewModel> _tagChips = [];
    private string _moreTags = string.Empty;
    private string _moreTagsToolTip = string.Empty;
    private IReadOnlyList<TagMenuItemViewModel> _tagMenu = [];

    /// <summary>Creates a card over a workstream.</summary>
    public WorkstreamCardViewModel(Workstream workstream) => _workstream = workstream;

    /// <summary>The workstream the card shows.</summary>
    public Workstream Workstream
    {
        get => _workstream;
        set
        {
            if (Set(ref _workstream, value))
            {
                Raise(nameof(Key));
                Raise(nameof(FaceTooltip));
                Raise(nameof(Title));
                Raise(nameof(ColorHex));
                Raise(nameof(JiraKeys));
                Raise(nameof(VisibleJiraKeys));
                Raise(nameof(MoreJiraKeys));
                Raise(nameof(AllJiraKeys));
                Raise(nameof(IsBlocked));
                Raise(nameof(IsPaused));
                Raise(nameof(FlagText));
                Raise(nameof(PauseTooltip));
                Raise(nameof(PauseMenuText));
                Raise(nameof(BlockMenuText));
                Raise(nameof(ArchiveTooltip));
                Raise(nameof(ReopenTomorrow));
                Raise(nameof(Column));
                Raise(nameof(IsArchived));
                Raise(nameof(ArchiveLabel));
                Raise(nameof(Tags));
            }
        }
    }

    /// <summary>The owner's tags on the card (card-search-and-tags item 9).</summary>
    public IReadOnlyList<string> Tags => _workstream.Tags;

    /// <summary>The card's repos and their branches, read with its branch line, for the search to match (item 2).</summary>
    public IReadOnlyList<CardRepo> Repos
    {
        get => _repos;
        set => Set(ref _repos, value);
    }

    /// <summary>What the search reads of the card (item 2): title, key, project, area, Jira keys, tags, branches, repos.</summary>
    public Theseus.Core.Finding.SearchableCard Searchable => new(
        Key, Title, _workstream.Project ?? string.Empty, Area, JiraKeys, Tags,
        [.. _repos.Select(repo => repo.Branch).OfType<string>()], [.. _repos.Select(repo => repo.Name)]);

    /// <summary>Whether a search is on that the card does not match, so it is dimmed (item 3, decision 1).</summary>
    public bool IsSearchDimmed
    {
        get => _isSearchDimmed;
        private set => Set(ref _isSearchDimmed, value);
    }

    /// <summary>Where the search's words fall in the title, to be highlighted (item 3); empty with no search.</summary>
    public IReadOnlyList<Theseus.Core.Finding.TextSpan> TitleHighlights
    {
        get => _titleHighlights;
        private set
        {
            if (Set(ref _titleHighlights, value))
            {
                Raise(nameof(HasTitleHighlights));
            }
        }
    }

    /// <summary>Whether the search highlights part of the title, so it wraps rather than trims (review R4).</summary>
    public bool HasTitleHighlights => _titleHighlights.Count > 0;

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

    /// <summary>Shows what the board's search makes of the card.</summary>
    internal void ShowSearch(bool dimmed, IReadOnlyList<Theseus.Core.Finding.TextSpan> highlights)
    {
        IsSearchDimmed = dimmed;

        if (!highlights.SequenceEqual(_titleHighlights))
        {
            TitleHighlights = highlights;
        }
    }

    /// <summary>The workstream's key, and the drag payload — a plain string, never a custom type.</summary>
    public string Key => _workstream.Key;

    /// <summary>The card heading and the tab title.</summary>
    public string Title => _workstream.Title;

    /// <summary>The permanent colour stripe.</summary>
    public string ColorHex => _workstream.ColorHex;

    /// <summary>Every Jira issue this workstream touches.</summary>
    public IReadOnlyList<string> JiraKeys => _workstream.JiraKeys;

    /// <summary>The ones the card shows, which is the first few of them.</summary>
    public IReadOnlyList<string> VisibleJiraKeys => JiraKeyDisplay.Visible(JiraKeys);

    /// <summary>What to say about the ones it does not, or nothing when they all fit.</summary>
    public string MoreJiraKeys => JiraKeyDisplay.More(JiraKeys);

    /// <summary>All of them, so hiding some does not lose them.</summary>
    public string AllJiraKeys => JiraKeyDisplay.All(JiraKeys);

    /// <summary>The pull requests this workstream mentions, with somewhere to open them.</summary>
    public IReadOnlyList<PullRequestLink> PullRequests
    {
        get => _pullRequests;
        set => Set(ref _pullRequests, value);
    }

    /// <summary>The card's Jira issues, with titles where a session reported them, for its drop-down.</summary>
    public IReadOnlyList<CardItemRow> JiraItems
    {
        get => _jiraItems;
        private set
        {
            if (Set(ref _jiraItems, value))
            {
                Raise(nameof(JiraButtonText));
            }
        }
    }

    /// <summary>The card's pull requests, with titles where a session reported them, for its drop-down.</summary>
    public IReadOnlyList<CardItemRow> PullRequestItems
    {
        get => _pullRequestItems;
        private set
        {
            if (Set(ref _pullRequestItems, value))
            {
                Raise(nameof(PullRequestButtonText));
            }
        }
    }

    /// <summary>
    /// The card's branch line, worded in Core (<see cref="Theseus.Core.Domain.BranchLines"/>): "on main", "on
    /// user/feature · 3 repos" or "branches differ"; empty when there is nothing to show.
    /// </summary>
    public string Branch
    {
        get => _branch;
        set => Set(ref _branch, value);
    }

    /// <summary>What hovering the branch line says: the branch of each of the card's repos.</summary>
    public string BranchTooltip
    {
        get => _branchTooltip;
        set => Set(ref _branchTooltip, value);
    }

    /// <summary>Whether the card's repos are not all on the same branch.</summary>
    public bool BranchesDiffer
    {
        get => _branchesDiffer;
        set => Set(ref _branchesDiffer, value);
    }

    /// <summary>What the Jira drop-down's button says.</summary>
    public string JiraButtonText => JiraItems.Count == 1 ? JiraItems[0].Id : $"Jira {JiraItems.Count}";

    /// <summary>What the pull request drop-down's button says.</summary>
    public string PullRequestButtonText => PullRequestItems.Count == 1 ? PullRequestItems[0].Id : $"PRs {PullRequestItems.Count}";

    /// <summary>Replaces the drop-downs' rows, only when they have changed so an open menu is left alone.</summary>
    public void SetItems(IReadOnlyList<CardItemRow> jira, IReadOnlyList<CardItemRow> pullRequests)
    {
        if (!jira.Select(row => row.Display + row.Url).SequenceEqual(JiraItems.Select(row => row.Display + row.Url)))
        {
            JiraItems = jira;
        }

        // The tooltip is compared too: a PR that cannot be opened says why there, and the reason changes with the
        // card's repos (card-branches review should-fix 2, 2026-11-19).
        if (!pullRequests.Select(row => row.Display + row.Url + row.Tooltip)
                .SequenceEqual(PullRequestItems.Select(row => row.Display + row.Url + row.Tooltip)))
        {
            PullRequestItems = pullRequests;
        }
    }

    /// <summary>Whether the work is blocked, shown as a flag rather than a column.</summary>
    public bool IsBlocked => _workstream.IsBlocked;

    /// <summary>Whether the work is paused: set aside on purpose, keeping its stage.</summary>
    public bool IsPaused => _workstream.IsPaused;

    /// <summary>What the card says about its flags: Paused, Blocked, both, or nothing.</summary>
    public string FlagText => (IsPaused, IsBlocked) switch
    {
        (true, true) => "Paused · blocked",
        (true, false) => "Paused",
        (false, true) => "Blocked",
        _ => string.Empty,
    };

    /// <summary>Whether a whole-board Start includes this workstream, ticked in the card's menu.</summary>
    public bool ReopenTomorrow => _workstream.ReopenTomorrow;

    /// <summary>Which column the card belongs to.</summary>
    public string Column => _workstream.Column;

    /// <summary>Which row the card sits in, by name.</summary>
    public string Area => _workstream.Area ?? string.Empty;

    /// <summary>Whether this workstream has been put away.</summary>
    public bool IsArchived => _workstream.IsArchived;

    /// <summary>What the archive button says, which is the opposite when it is already away.</summary>
    public string ArchiveLabel => IsArchived ? "Restore" : "Archive";

    /// <summary>What the session is doing right now.</summary>
    /// <summary>The question its session has open for the owner (1.6), or null; it is what the status line says while waiting.</summary>
    public Theseus.Core.Questions.OwnerQuestion? Question
    {
        get => _question;
        set
        {
            if (Set(ref _question, value))
            {
                RaiseStatus();
            }
        }
    }

    private void RaiseStatus()
    {
        Raise(nameof(StatusLine));
        Raise(nameof(StatusToolTip));
    }

    /// <summary>The status line's tooltip: the question with its options and why, when it asks one.</summary>
    public string StatusToolTip => Question is { } question && Lifecycle == SessionLifecycle.NeedsAttention
        ? Theseus.Core.Questions.QuestionWords.Detail(question)
        : StatusLine;

    public SessionLifecycle Lifecycle
    {
        get => _lifecycle;
        set
        {
            if (Set(ref _lifecycle, value))
            {
                Raise(nameof(PrimaryAction));
                Raise(nameof(PlayTooltip));
                Raise(nameof(PauseTooltip));
                Raise(nameof(IsLive));
                Raise(nameof(ShowStop));
                Raise(nameof(IsWorking));
                Raise(nameof(IsRunning));
                RaiseStatus();
            }
        }
    }

    /// <summary>
    /// Whether a stop is under way for this card's session, from the moment it is asked until the session exits or
    /// the request is retired. The Stop icon gives way to a stopping icon that cannot be pressed, so a stop cannot be
    /// asked twice from the card (close-step AC 5).
    /// </summary>
    public bool IsStopping
    {
        get => _isStopping;
        set
        {
            if (Set(ref _isStopping, value))
            {
                Raise(nameof(ShowStop));
            }
        }
    }

    /// <summary>Whether the card offers its Stop icon: a live session with no stop under way.</summary>
    public bool ShowStop => IsLive && !IsStopping;

    /// <summary>Where the stop under way has got to, in the spec's words (close-step AC 6).</summary>
    public string StopTooltip
    {
        get => _stopTooltip;
        set => Set(ref _stopTooltip, value);
    }

    /// <summary>
    /// Whether Close now is offered: /exit was tried and typed nothing for certain. It tries once, only when pressed,
    /// and raises the terminal as it does (close-step AC 9).
    /// </summary>
    public bool CanCloseNow
    {
        get => _canCloseNow;
        set => Set(ref _canCloseNow, value);
    }

    /// <summary>Tries /exit once more for a stop whose last attempt typed nothing.</summary>
    public RelayCommand? CloseNowCommand { get; set; }

    /// <summary>When the current lifecycle state began, for the card's relative time line.</summary>
    public DateTimeOffset? LifecycleSinceUtc
    {
        get => _lifecycleSinceUtc;
        set
        {
            if (Set(ref _lifecycleSinceUtc, value))
            {
                RaiseStatus();
            }
        }
    }

    /// <summary>For a card not started today, when its last run ended, where that was recorded.</summary>
    public DateTimeOffset? LastExitedUtc
    {
        get => _lastExitedUtc;
        set
        {
            if (Set(ref _lastExitedUtc, value))
            {
                RaiseStatus();
            }
        }
    }

    /// <summary>For a card not started today, when its last run began.</summary>
    public DateTimeOffset? LastStartedUtc
    {
        get => _lastStartedUtc;
        set
        {
            if (Set(ref _lastStartedUtc, value))
            {
                RaiseStatus();
            }
        }
    }

    /// <summary>
    /// Whether this card should show movement: a session that is working, when movement is wanted at
    /// all. Held here rather than read from the system so that a board out of focus, or a machine
    /// where animation is switched off, simply stops moving.
    /// </summary>
    public bool IsWorking => _motionAllowed && Lifecycle == SessionLifecycle.Running;

    /// <summary>
    /// Whether a session is working, whatever the board is doing about movement.
    ///
    /// Separate from <see cref="IsWorking"/> because the two answer different questions. The banner
    /// says a session is running and must stay put when the window loses focus or the machine has
    /// animation switched off; only the movement across it is a matter of taste and battery.
    /// </summary>
    public bool IsRunning => Lifecycle == SessionLifecycle.Running;

    /// <summary>Whether movement is wanted at all right now.</summary>
    public bool MotionAllowed
    {
        get => _motionAllowed;
        set
        {
            if (Set(ref _motionAllowed, value))
            {
                Raise(nameof(IsWorking));
            }
        }
    }

    /// <summary>
    /// The name of a session running for this workstream that the board is not following, when there
    /// is one. A workstream whose session was restarted outside Theseus reads as finished otherwise,
    /// which is the one case where a card is wrong rather than merely quiet.
    /// </summary>
    public string LostSessionName
    {
        get => _lostSessionName;
        set => Set(ref _lostSessionName, value);
    }

    /// <summary>Attaches this workstream to the session running unattached for it.</summary>
    public RelayCommand? AdoptLostCommand { get; set; }

    /// <summary>Whether a session is running, which is what disables Restart in favour of Focus.</summary>
    public bool IsLive => Lifecycle
        is SessionLifecycle.Launching
        or SessionLifecycle.Running
        or SessionLifecycle.Idle
        or SessionLifecycle.NeedsAttention;

    /// <summary>Whether this card is the one the next Adopt will attach a session to.</summary>
    public bool IsAdoptTarget
    {
        get => _isAdoptTarget;
        set => Set(ref _isAdoptTarget, value);
    }

    /// <summary>
    /// Whether the user has ticked this card for a bulk Start or Stop.
    ///
    /// This is the whole of how Theseus decides what "start these" means. It replaced a rule that
    /// worked out which cards to start from which column they were in, which was a guess about the
    /// board's shape and got it wrong for any board whose last column was not the only one to leave
    /// alone.
    /// </summary>
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

    /// <summary>
    /// Raised when this card is ticked or unticked, so the buttons that act on a selection can say
    /// how many they would affect and refuse when that is none.
    /// </summary>
    public Action? SelectionChanged { get; set; }

    /// <summary>A drag of the card carries its key.</summary>
    public string DragPayload => Key;

    /// <summary>
    /// Whether the card is folded down to its title.
    ///
    /// Not persisted, and not the same question as whether the row is collapsed. This is a view of
    /// the board taken on demand — "show me what is running" — and a board that reopened with cards
    /// folded from yesterday would be hiding work for a reason nobody could remember.
    /// </summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => Set(ref _isCollapsed, value);
    }

    /// <summary>
    /// What the session says it is working on, taken from the list it keeps for itself. Busy and
    /// idle say whether a session is thinking; this says what about, which is what decides whether
    /// a tab is worth opening.
    /// </summary>
    public string TodoSummary
    {
        get => _todoSummary;
        set => Set(ref _todoSummary, value);
    }

    /// <summary>
    /// What the running session is using: processor, memory, I/O and agents at work. Empty while
    /// the session is not running.
    /// </summary>
    public string Resources
    {
        get => _resources;
        set => Set(ref _resources, value);
    }

    /// <summary>The processes behind <see cref="Resources"/>, busiest first.</summary>
    public string ResourcesTooltip
    {
        get => _resourcesTooltip;
        set => Set(ref _resourcesTooltip, value);
    }

    /// <summary>
    /// What the running session calls itself, when that is no longer what the card calls it. A tab
    /// cannot be renamed once it is open, so a rename shows here until the session is restarted.
    /// </summary>
    public string RunningAs
    {
        get => _runningAs;
        set => Set(ref _runningAs, value);
    }

    /// <summary>
    /// What this workstream shares with another that it probably should not - most importantly a
    /// handover document, where two sessions will overwrite one another in silence.
    /// </summary>
    public string Clash
    {
        get => _clash;
        set => Set(ref _clash, value);
    }

    /// <summary>
    /// The Jira issues this card shares with cards it is linked to. Deliberate, so said quietly rather
    /// than as a clash, but kept visible.
    /// </summary>
    public string LinkedShare
    {
        get => _linkedShare;
        set => Set(ref _linkedShare, value);
    }

    /// <summary>The card's one line about its links: whether it is ready or waiting, and where it came from.</summary>
    public LinkLineText Links
    {
        get => _links;
        set
        {
            if (Set(ref _links, value))
            {
                Raise(nameof(LinkLine));
                Raise(nameof(LinkTooltip));
                Raise(nameof(IsLinkReady));
            }
        }
    }

    /// <summary>The link line's text; empty for a card with no links.</summary>
    public string LinkLine => _links.Text;

    /// <summary>Every link on the card, one per line.</summary>
    public string LinkTooltip => _links.Tooltip;

    /// <summary>Whether every input has been accepted and the card has not run since. Shown, never acted on.</summary>
    public bool IsLinkReady => _links.IsReady;

    /// <summary>Whether the card's newest run ended unexpectedly, which turns a missing report into a lost one.</summary>
    public bool LastRunCrashed { get; set; }

    /// <summary>Whether the card sits in the board's last stage.</summary>
    public bool IsInLastStage { get; set; }

    /// <summary>Whether the pointer is over a card linked to this one, so it is outlined.</summary>
    public bool IsLinkHighlighted
    {
        get => _isLinkHighlighted;
        set => Set(ref _isLinkHighlighted, value);
    }

    /// <summary>
    /// Whether a stop request for this session is written and not yet collected.
    ///
    /// It is shown because the wait is otherwise invisible and can run to minutes: a working session
    /// is handed its request when its turn ends, and until then a board that has been asked looks
    /// exactly like one that has not.
    /// </summary>
    public bool StopPending
    {
        get => _stopPending;
        set
        {
            if (Set(ref _stopPending, value))
            {
                RaiseStatus();
            }
        }
    }

    /// <summary>The card third line: what the session is doing, and for how long.</summary>
    public string StatusLine => StopPending
        ? Lifecycle switch
        {
            SessionLifecycle.Idle => "stopping — waiting for the turn it was given to end",
            SessionLifecycle.NeedsAttention => "stopping — answer it first",
            _ => "stopping when this turn ends — press Stop again to cut in",
        }
        : StopReason is { } reason && !IsLive
            ? reason.Line
            : Lifecycle switch
        {
        SessionLifecycle.NotStarted => CardLifecycle.NotStartedLine(LastExitedUtc, LastStartedUtc, DateTimeOffset.UtcNow),
        SessionLifecycle.Launching => "launching",
        SessionLifecycle.Running => Since("busy"),
        SessionLifecycle.Idle => Since("idle"),
        SessionLifecycle.NeedsAttention => Question is { } question
            ? Theseus.Core.Questions.QuestionWords.Card(question)
            : Since("waiting for you"),
        SessionLifecycle.Ended => Since("ended"),
        SessionLifecycle.Ghost => "ended unexpectedly",
        _ => string.Empty,
    };

    /// <summary>
    /// What resuming this session would cost, for the button that would do it. Empty while the
    /// session is running, when Restart is not on offer and the number would only be noise.
    /// </summary>
    public string RestartCost
    {
        get => _restartCost;
        set => Set(ref _restartCost, value);
    }

    /// <summary>Whether there is a conversation on disk for Resume to reopen.</summary>
    public bool CanResume { get; set; }

    /// <summary>What resuming would cost, for the menu entry that would do it.</summary>
    public string ResumeTooltip
    {
        get => _resumeTooltip;
        set => Set(ref _resumeTooltip, value);
    }

    /// <summary>Starts this workstream by reopening its last conversation, whatever the board's default.</summary>
    public RelayCommand? ResumeCommand
    {
        get => _resumeCommand;
        set => Set(ref _resumeCommand, value);
    }

    /// <summary>Starts this workstream in a new conversation pointed at its handoff, whatever the board's default.</summary>
    public RelayCommand? StartFreshCommand
    {
        get => _startFreshCommand;
        set => Set(ref _startFreshCommand, value);
    }

    /// <summary>Marks the work paused, or not, from the card's menu.</summary>
    public RelayCommand? TogglePausedCommand { get; set; }

    /// <summary>Marks the work blocked, or not, from the card's menu.</summary>
    public RelayCommand? ToggleBlockedCommand
    {
        get => _toggleBlockedCommand;
        set => Set(ref _toggleBlockedCommand, value);
    }

    /// <summary>Includes the workstream in a whole-board Start, or not, from the card's menu.</summary>
    public RelayCommand? ToggleReopenTomorrowCommand
    {
        get => _toggleReopenTomorrowCommand;
        set => Set(ref _toggleReopenTomorrowCommand, value);
    }

    /// <summary>The play button's tooltip: what it does, and what resuming would cost.</summary>
    public string PlayTooltip => StopReason?.Kind switch
    {
        Theseus.Core.Projects.WhyStoppedKind.Done when !IsLive => "Restart for more work: asks what the more work is",
        Theseus.Core.Projects.WhyStoppedKind.ProjectStopped when !IsLive => "Start its project as it was when stopped",
        _ => string.IsNullOrWhiteSpace(RestartCost)
            ? $"{PrimaryAction} this workstream's session"
            : $"{PrimaryAction} this workstream's session. {RestartCost}",
    };

    /// <summary>
    /// Why it is stopped, when that is more than its own line says (1.1e AC 12): done, its project stopped, or stopped by
    /// the owner while its project ran. Set by the shell with the lifecycles; null while it runs.
    /// </summary>
    public Theseus.Core.Projects.WhyStopped? StopReason
    {
        get => _stopReason;
        set
        {
            if (Set(ref _stopReason, value))
            {
                RaiseStatus();
                Raise(nameof(PrimaryAction));
                Raise(nameof(PlayTooltip));
                Raise(nameof(IsDone));
            }
        }
    }

    /// <summary>Whether it is done: its face is muted, as a pinned project's is, and it stays where the owner put it.</summary>
    public bool IsDone => _stopReason?.Kind == Theseus.Core.Projects.WhyStoppedKind.Done;

    /// <summary>The pause button's tooltip, which depends on whether a session is running.</summary>
    public string PauseTooltip => IsLive
        ? "Pause: stop this session with a handoff, and mark the card paused"
        : IsPaused
            ? "Paused. Click to clear the mark"
            : "Mark as paused: set aside on purpose, keeping its stage";

    /// <summary>What the card's menu offers for pausing: an action, so it says what clicking does.</summary>
    public string PauseMenuText => IsPaused ? "Un_pause" : "_Pause";

    /// <summary>What the card's menu offers for blocking.</summary>
    public string BlockMenuText => IsBlocked ? "Un_block" : "_Block";

    /// <summary>The archive button's tooltip.</summary>
    public string ArchiveTooltip => IsArchived
        ? "Restore this workstream to the board"
        : "Archive: take this workstream off the board without losing anything";

    /// <summary>Pauses the card: stops a running session with a handoff and marks it paused.</summary>
    public RelayCommand? PauseCommand { get; set; }

    /// <summary>The card menu's Pause or Unpause, which acts on the selection when this card is in it.</summary>
    public RelayCommand? MenuPauseCommand { get; set; }

    /// <summary>Opens the card's properties, from its menu or Alt+Enter.</summary>
    public RelayCommand? PropertiesCommand { get; set; }

    /// <summary>Creates a verifier card for this card's change, from its menu (verifier-card AC 9).</summary>
    public RelayCommand? VerifyWithHoldoutsCommand { get; set; }

    /// <summary>
    /// Verify with holdouts…'s tooltip on the card's menu: what it does, or, while it is off, why (1.1e AC 6a), as the
    /// shell last found it.
    /// </summary>
    public string VerifyToolTip
    {
        get => _verifyToolTip;
        set => Set(ref _verifyToolTip, value);
    }

    /// <summary>When its project was granted branch rules, while they apply, or null (1.1e AC 12b).</summary>
    public DateTimeOffset? BranchRulesSince
    {
        get => _branchRulesSince;
        set
        {
            if (Set(ref _branchRulesSince, value))
            {
                Raise(nameof(BranchRulesMark));
                Raise(nameof(BranchRulesToolTip));
            }
        }
    }

    /// <summary>"project branch rules" while they apply, else empty: a small mark on the card.</summary>
    public string BranchRulesMark => _branchRulesSince is null ? string.Empty : Theseus.Core.Projects.ProjectBranchRules.Mark;

    /// <summary>The mark's tooltip, with the grant's date.</summary>
    public string BranchRulesToolTip => _branchRulesSince is { } since
        ? $"Granted by you on {since.ToLocalTime():d MMM yyyy}: this card may branch off its project's primary branch, push, and merge back without asking"
        : string.Empty;

    /// <summary>Add to project…: adds the card's row to a project's file (1.1c AC 7).</summary>
    public RelayCommand? AddToProjectCommand { get; set; }

    /// <summary>Remove from project: removes the card's rows from its project's file (1.1c AC 7).</summary>
    public RelayCommand? RemoveFromProjectCommand { get; set; }

    /// <summary>Convert to project…: a new project with this card as its first workstream (1.1c AC 7).</summary>
    public RelayCommand? ConvertToProjectCommand { get; set; }

    /// <summary>
    /// Where its project slug's work was read, branch and commit (project-view AC 5); empty for a card in no readable
    /// project.
    /// </summary>
    public string ProjectSource
    {
        get => _projectSource;
        set
        {
            if (Set(ref _projectSource, value))
            {
                Raise(nameof(FaceTooltip));
            }
        }
    }

    /// <summary>The card face's tooltip: its working directory, and for a project card where its work was read.</summary>
    public string FaceTooltip => _projectSource.Length == 0
        ? _workstream.WorkingDirectory
        : $"{_workstream.WorkingDirectory}{Environment.NewLine}{_projectSource}";

    /// <summary>
    /// The handover file name the card's menu offers to rename to, to match its title, or empty when none is offered
    /// (handoff-names AC 5).
    /// </summary>
    public string HandoffRenameTo
    {
        get => _handoffRenameTo;
        set
        {
            if (Set(ref _handoffRenameTo, value))
            {
                Raise(nameof(HandoffRenameMenuText));
            }
        }
    }

    /// <summary>What the menu item says.</summary>
    public string HandoffRenameMenuText => $"Rename handover to {System.IO.Path.GetFileName(_handoffRenameTo)}";

    /// <summary>Renames the card's handover file to match its title (handoff-names AC 6).</summary>
    public RelayCommand? RenameHandoffCommand { get; set; }

    /// <summary>Opens the New dialog with this card as the parent, from the card's menu.</summary>
    public RelayCommand? SpawnFollowOnCommand
    {
        get => _spawnFollowOnCommand;
        set => Set(ref _spawnFollowOnCommand, value);
    }

    /// <summary>
    /// Which model the session is running — "Opus 5", "Fable 5.1" — read off the end of its
    /// transcript, which is the only place it is recorded. Empty when there is no transcript yet.
    /// </summary>
    public string Model
    {
        get => _model;
        set => Set(ref _model, value);
    }

    /// <summary>The single button the card offers on hover.</summary>
    public string PrimaryAction => IsLive ? "Focus" : StopReason?.Kind switch
    {
        Theseus.Core.Projects.WhyStoppedKind.Done => "Restart for more work",
        Theseus.Core.Projects.WhyStoppedKind.ProjectStopped => "Start its project",
        _ => DefaultAction,
    };

    private string DefaultAction => Lifecycle switch
    {
        SessionLifecycle.NotStarted => "Start",
        SessionLifecycle.Ended or SessionLifecycle.Ghost => "Restart",
        _ => "Focus",
    };

    private string Since(string verb)
    {
        var elapsed = Converters.RelativeTimeConverter.Format(LifecycleSinceUtc, DateTimeOffset.UtcNow);

        return string.IsNullOrEmpty(elapsed) ? verb : $"{verb} {elapsed}";
    }
}
