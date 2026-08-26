using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One card on the board.</summary>
public sealed class WorkstreamCardViewModel : ObservableObject
{
    private Workstream _workstream;
    private SessionLifecycle _lifecycle = SessionLifecycle.NotStarted;
    private string _todoSummary = string.Empty;
    private bool _motionAllowed = true;
    private string _lostSessionName = string.Empty;
    private bool _isSelected;
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
                Raise(nameof(IsBlocked));
                Raise(nameof(Column));
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
    public bool IsSelected
    {
        get => _isSelected;
        set => Set(ref _isSelected, value);
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

    /// <summary>The card third line: what the session is doing, and for how long.</summary>
    public string StatusLine => Lifecycle switch
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
