using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One card on the board.</summary>
public sealed class WorkstreamCardViewModel : ObservableObject
{
    private Workstream _workstream;
    private SessionLifecycle _lifecycle = SessionLifecycle.NotStarted;
    private string _todoSummary = string.Empty;
    private bool _stopPending;
    private string _runningAs = string.Empty;
    private string _clash = string.Empty;
    private string _restartCost = string.Empty;
    private bool _motionAllowed = true;
    private string _lostSessionName = string.Empty;
    private bool _isSelected;
    private bool _isAdoptTarget;
    private IReadOnlyList<PullRequestLink> _pullRequests = [];
    private DateTimeOffset? _lifecycleSinceUtc;

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
                Raise(nameof(Title));
                Raise(nameof(ColorHex));
                Raise(nameof(JiraKeys));
                Raise(nameof(VisibleJiraKeys));
                Raise(nameof(MoreJiraKeys));
                Raise(nameof(AllJiraKeys));
                Raise(nameof(IsBlocked));
                Raise(nameof(Column));
                Raise(nameof(IsArchived));
                Raise(nameof(ArchiveLabel));
            }
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

    /// <summary>Whether the work is blocked, shown as a flag rather than a column.</summary>
    public bool IsBlocked => _workstream.IsBlocked;

    /// <summary>Which column the card belongs to.</summary>
    public string Column => _workstream.Column;

    /// <summary>Which row the card sits in, by name.</summary>
    public string Area => _workstream.Area ?? string.Empty;

    /// <summary>Whether this workstream has been put away.</summary>
    public bool IsArchived => _workstream.IsArchived;

    /// <summary>What the archive button says, which is the opposite when it is already away.</summary>
    public string ArchiveLabel => IsArchived ? "Restore" : "Archive";

    /// <summary>What the session is doing right now.</summary>
    public SessionLifecycle Lifecycle
    {
        get => _lifecycle;
        set
        {
            if (Set(ref _lifecycle, value))
            {
                Raise(nameof(PrimaryAction));
                Raise(nameof(IsLive));
                Raise(nameof(IsWorking));
                Raise(nameof(IsRunning));
                Raise(nameof(StatusLine));
            }
        }
    }

    /// <summary>When the current lifecycle state began, for the card's relative time line.</summary>
    public DateTimeOffset? LifecycleSinceUtc
    {
        get => _lifecycleSinceUtc;
        set
        {
            if (Set(ref _lifecycleSinceUtc, value))
            {
                Raise(nameof(StatusLine));
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
                Raise(nameof(StatusLine));
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
        : Lifecycle switch
        {
        SessionLifecycle.NotStarted => "not started today",
        SessionLifecycle.Launching => "launching",
        SessionLifecycle.Running => Since("busy"),
        SessionLifecycle.Idle => Since("idle"),
        SessionLifecycle.NeedsAttention => Since("waiting for you"),
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

    /// <summary>The single button the card offers on hover.</summary>
    public string PrimaryAction => Lifecycle switch
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
