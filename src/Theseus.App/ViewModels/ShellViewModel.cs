using System.Diagnostics;
using System.IO;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Colour;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;
using Theseus.Core.Settings;
using Theseus.Core.Stopping;
using Theseus.Core.Usage;

namespace Theseus.App.ViewModels;

/// <summary>The window's own state: what it shows, what it can start, and how it is painted.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly LaunchCoordinator _launcher;
    private readonly IPlanStore _plans;
    private readonly ITabColourAllocator _colours;
    private readonly PullRequestResolver _pullRequests;
    private readonly List<StopStep> _stopQueue = [];
    private readonly IStopRequestStore _stopRequests = new StopRequestStore(StopRequestRoot.Default);
    private readonly IRunLog _runs;
    private readonly ISessionTodoReader _todos;
    private readonly RunEndings _endings;
    private readonly IWorkflowActivityReader _activity =
        new WorkflowActivityReader(WorkflowRunReader.DefaultDirectory);
    private readonly ClaudeAccountSource _account = new();
    private AppTheme _theme;
    private string _notice = string.Empty;
    private bool _isSessionsPanelOpen;
    private bool _motionAllowed = true;

    /// <summary>
    /// The sessions already told to exit. Typing it twice would land in a session that had moved on
    /// to something else, and there is no taking a keystroke back.
    /// </summary>
    private readonly HashSet<string> _closed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stops that were asked for before this Theseus started, and have not been agreed to yet.
    ///
    /// A request survives the board being closed on purpose: the session was asked to stop, wrote its
    /// handoff, and nothing ever typed the exit. But completing it silently on the next launch means
    /// starting the board kills sessions within a second, with no chance to change your mind — which
    /// is what happened to two of Nigel's on 2026-09-02, 0.8s after startup. So they wait here until
    /// he says.
    /// </summary>
    private readonly HashSet<string> _unconfirmed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Workstreams a Start held back because a tab of their own name is already open with nothing
    /// running in it. They wait for an answer rather than opening a second tab beside the first.
    /// </summary>
    private IReadOnlyList<Workstream> _heldForTabs = [];

    /// <summary>How the held workstreams were asked to start, so Start anyway honours it.</summary>
    private StartMode? _heldStartMode;

    /// <summary>
    /// Workstreams a Start held back because they still wait on an input. A fresh session with
    /// nothing to read only goes looking for a document that does not exist yet.
    /// </summary>
    private IReadOnlyList<Workstream> _heldForInputs = [];

    /// <summary>How the waiting workstreams were asked to start.</summary>
    private StartMode? _heldForInputsMode;

    /// <summary>Creates the shell over everything the window drives.</summary>
    public ShellViewModel(
        BoardViewModel board,
        SessionsViewModel sessions,
        ISettingsStore settings,
        LaunchCoordinator launcher,
        IPlanStore plans,
        IRunLog runs,
        ISessionTodoReader todos,
        ITabColourAllocator colours,
        RunEndings endings)
    {
        Board = board;
        _runs = runs;
        _todos = todos;
        _endings = endings;
        Sessions = sessions;
        _settings = settings;
        _launcher = launcher;
        _plans = plans;
        _colours = colours;
        _pullRequests = new PullRequestResolver(new GitRemoteReader());
        _theme = settings.Read().Theme;
        _isSessionsPanelOpen = !settings.Read().SessionsPanelCollapsed;

        RefreshCommand = new RelayCommand(_ => Refresh());
        FocusCardCommand = new RelayCommand(FocusCard, CanFocusCard);
        OpenJiraCommand = new RelayCommand(OpenJira, parameter => parameter is string);
        OpenPullRequestCommand = new RelayCommand(OpenPullRequest,
            parameter => parameter is PullRequestLink { Url: not null });

        StartCardCommand = new RelayCommand(StartCard, CanStartCard);
        SelectCardCommand = new RelayCommand(ShowCard);
        ShowCardLinksCommand = new RelayCommand(parameter => ShowCard(parameter, DrawerTab.Links));
        ToggleSessionsPanelCommand = new RelayCommand(_ => IsSessionsPanelOpen = !IsSessionsPanelOpen);
        StopCardCommand = new RelayCommand(parameter => BeginStop(CardSteps(parameter)),
            parameter => CardSteps(parameter).Count > 0);

        StartSelectedCommand = new RelayCommand(_ => StartMany(ChosenToStart()),
            _ => ChosenToStart().Count > 0);
        StopSelectedCommand = new RelayCommand(_ => BeginStop(ChosenSteps()),
            _ => ChosenSteps().Count > 0);
        SelectAllCommand = new RelayCommand(_ => ChooseAll(true), _ => Board.Cards.Any(card => !card.IsSelected));
        ClearSelectionCommand = new RelayCommand(_ => ChooseAll(false), _ => Board.Cards.Any(card => card.IsSelected));
        InvertSelectionCommand = new RelayCommand(_ => Invert(), _ => Board.Cards.Any());

        // Archive is on the card as well as in the details; deleting is not. One is reversible and
        // the everyday way to clear finished work off the board, and the other destroys a plan file
        // with no undo. A destructive button one click from Start, on a card somebody is scanning
        // past, is not a button - it is a trap.
        ArchiveCardCommand = new RelayCommand(ArchiveCard,
            parameter => parameter is WorkstreamCardViewModel { IsLive: false });

        Drawer = new DetailDrawerViewModel(plans, runs, new WorkflowRunReader(WorkflowRunReader.DefaultDirectory),
            card =>
            {
                Board.Persist(card);
                Refresh();
            },
            (card, archived) =>
            {
                Board.SetArchived(card, archived);
                Refresh();
                Notice = archived
                    ? $"Archived {card.Title}. It is under Archived until you restore it."
                    : $"Restored {card.Title}.";
            },
            card =>
            {
                var title = card.Title;
                var key = card.Key;

                Board.Remove(card);
                Chains?.Deleted(key);
                Refresh();
                Notice = $"Deleted {title}.";
            });

        Settings = new SettingsViewModel(settings, Refresh);

        NewWorkstream = new NewWorkstreamViewModel(plans, colours,
            () => [.. Board.Stages],
            () => [.. Board.Rows.Select(row => row.Area)],
            created =>
            {
                Refresh();
                Board.Place(created.Key, created.Stage, created.Area);

                if (created.ParentKey is { } parent)
                {
                    Chains?.FollowedOn(parent, created.Key, created.ReportTo);
                }

                if (created.ProposalId is { } proposal)
                {
                    Chains?.Proposals?.Approved(proposal, created.Key);
                }

                if (Board.Card(created.Key) is not { } card)
                {
                    Notice = "The workstream was created but is not on the board yet.";

                    return;
                }

                if (created.StartNow)
                {
                    StartMany([card.Workstream]);

                    return;
                }

                Notice = $"Created {card.Title} in {card.Column}.";
            },
            () => Chains?.TakenKeys ?? [],
            () => _settings.Read().BriefFolderName);

        SpawnFollowOnCommand = new RelayCommand(
            parameter => NewWorkstream.OpenFollowOn(((WorkstreamCardViewModel)parameter!).Workstream),
            parameter => parameter is WorkstreamCardViewModel { IsArchived: false });

        Instructions = new InstructionsViewModel();

        DayReport = new DayReportViewModel(runs, () => Workstreams(),
            sessionId => Sessions.Snapshot.WithSessionId(sessionId).Count > 0);

        // Anything already delivered when the board opens is a stop nobody finished. Held back until
        // agreed to; anything asked for from now on runs its course as normal.
        foreach (var request in _stopRequests.Handed())
        {
            _unconfirmed.Add(request.SessionId);
        }

        ConfirmStopsCommand = new RelayCommand(_ => ConfirmStops());
        AbandonStopsCommand = new RelayCommand(_ => AbandonStops());

        StartAnywayCommand = new RelayCommand(_ =>
        {
            var held = _heldForTabs;
            var mode = _heldStartMode;

            _heldForTabs = [];
            TabsChanged();
            StartMany(held, evenIfTabsExist: true, evenIfWaiting: true, startMode: mode);
        });

        StartWithoutInputsCommand = new RelayCommand(_ =>
        {
            var held = _heldForInputs;
            var mode = _heldForInputsMode;

            _heldForInputs = [];
            InputsChanged();
            StartMany(held, evenIfWaiting: true, startMode: mode, trigger: "StartWithoutInputs");
        });

        SkipWaitingCommand = new RelayCommand(_ =>
        {
            var count = _heldForInputs.Count;

            _heldForInputs = [];
            InputsChanged();
            Notice = count == 1 ? "Left the waiting card unstarted." : $"Left {count} waiting cards unstarted.";
        });

        KeepExistingTabsCommand = new RelayCommand(_ =>
        {
            var count = _heldForTabs.Count;

            _heldForTabs = [];
            TabsChanged();
            Notice = count == 1 ? "Left its tab alone." : $"Left {count} tabs alone.";
        });

        CollapseInactiveCommand = new RelayCommand(_ => Collapse(true),
            _ => Board.Cards.Any(card => !card.IsLive && !card.IsCollapsed));
        ExpandAllCommand = new RelayCommand(_ => Collapse(false),
            _ => Board.Cards.Any(card => card.IsCollapsed));

        WireLanes();
        Board.LanesChanged += (_, _) => WireLanes();
    }

    /// <summary>
    /// Reconnects what a rebuild replaces: every card tells the shell when it is ticked, and every
    /// row keeps its collapse button.
    /// </summary>
    private void WireLanes()
    {
        foreach (var card in Board.Cards)
        {
            card.SelectionChanged = SelectionMoved;
        }

        foreach (var row in Board.Rows)
        {
            var captured = row;
            captured.ToggleCommand = new RelayCommand(_ => Board.ToggleRow(captured));
        }
    }


    /// <summary>The board this window shows.</summary>
    public BoardViewModel Board { get; }

    /// <summary>The sessions running right now.</summary>
    public SessionsViewModel Sessions { get; }

    /// <summary>The account usage shown in the status bar.</summary>
    public UsageViewModel? Usage { get; set; }

    /// <summary>The machine's locks, for the strip under the command bar and the status bar.</summary>
    public LocksViewModel? Locks { get; set; }

    /// <summary>The links between workstreams: the Chains tab, the cards' link lines and the drawer's Related section.</summary>
    public ChainsViewModel? Chains { get; set; }

    /// <summary>Where the board publishes itself at the end of each refresh, for the session tools to read.</summary>
    public BoardSnapshotSource? Snapshots { get; set; }

    /// <summary>The offer of a newer published build, under the notice line.</summary>
    public UpdateViewModel? Updates { get; set; }

    /// <summary>Opens the New dialog for a follow-on spawned from a card.</summary>
    public RelayCommand SpawnFollowOnCommand { get; }

    /// <summary>Starts the held-back waiting cards anyway, without their inputs.</summary>
    public RelayCommand StartWithoutInputsCommand { get; }

    /// <summary>Leaves the waiting cards unstarted.</summary>
    public RelayCommand SkipWaitingCommand { get; }

    /// <summary>Whether a Start is waiting on an answer about cards whose inputs are not accepted yet.</summary>
    public bool HasHeldForInputs => _heldForInputs.Count > 0;

    /// <summary>What the strip says: which cards are waiting, and on what.</summary>
    public string HeldForInputsSummary => Chains?.WaitingSummary(_heldForInputs) ?? string.Empty;

    /// <summary>Re-reads everything on disk.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>Brings a card's session to the front.</summary>
    public RelayCommand FocusCardCommand { get; }

    /// <summary>Opens a Jira issue in the browser.</summary>
    public RelayCommand OpenJiraCommand { get; }

    /// <summary>Opens a pull request in the browser.</summary>
    public RelayCommand OpenPullRequestCommand { get; }

    /// <summary>Starts one workstream.</summary>
    public RelayCommand StartCardCommand { get; }

    /// <summary>Starts the ticked cards that can be started.</summary>
    public RelayCommand StartSelectedCommand { get; }

    /// <summary>Ticks every card on the board.</summary>
    public RelayCommand SelectAllCommand { get; }

    /// <summary>Unticks every card.</summary>
    public RelayCommand ClearSelectionCommand { get; }

    /// <summary>Ticks what is not ticked and unticks what is.</summary>
    public RelayCommand InvertSelectionCommand { get; }

    /// <summary>Folds every card with no live session down to its title.</summary>
    public RelayCommand CollapseInactiveCommand { get; }

    /// <summary>Opens every card again.</summary>
    public RelayCommand ExpandAllCommand { get; }

    /// <summary>Starts the held-back workstreams anyway, opening a second tab for each.</summary>
    public RelayCommand StartAnywayCommand { get; }

    /// <summary>Drops them, leaving their existing tabs alone.</summary>
    public RelayCommand KeepExistingTabsCommand { get; }

    /// <summary>Whether a Start is waiting on an answer about tabs already open.</summary>
    public bool HasHeldForTabs => _heldForTabs.Count > 0;

    /// <summary>What the strip says: which workstreams already have a tab, and how many.</summary>
    public string HeldForTabsSummary
    {
        get
        {
            if (_heldForTabs.Count == 0)
            {
                return string.Empty;
            }

            var names = string.Join(" · ", _heldForTabs.Select(workstream => workstream.Title));

            return _heldForTabs.Count == 1
                ? $"{names} already has a tab open, with no session running in it."
                : $"{_heldForTabs.Count} workstreams already have a tab open with no session running in it: {names}.";
        }
    }

    /// <summary>Goes ahead with the stops that were asked for before the board opened.</summary>
    public RelayCommand ConfirmStopsCommand { get; }

    /// <summary>Drops them, leaving the sessions running.</summary>
    public RelayCommand AbandonStopsCommand { get; }

    /// <summary>Whether there are unfinished stops waiting to be agreed to.</summary>
    public bool HasUnconfirmedStops => UnconfirmedStops().Count > 0;

    /// <summary>What the strip says: how many, and which workstreams they belong to.</summary>
    public string UnconfirmedStopsSummary
    {
        get
        {
            var waiting = UnconfirmedStops();

            if (waiting.Count == 0)
            {
                return string.Empty;
            }

            var names = string.Join(" · ", waiting.Select(card => card.Title));

            return waiting.Count == 1
                ? $"{names} was asked to stop before Theseus last closed."
                : $"{waiting.Count} sessions were asked to stop before Theseus last closed: {names}.";
        }
    }

    /// <summary>How many cards are ticked, so the buttons can say what they would act on.</summary>
    public string SelectionSummary => Board.Cards.Count(card => card.IsSelected) is var count && count > 0
        ? $"{count} selected"
        : "none selected";

    /// <summary>Opens a card in the detail drawer.</summary>
    public RelayCommand SelectCardCommand { get; }

    /// <summary>Opens a card's details on its Links tab, from its link line or the Chains panel.</summary>
    public RelayCommand ShowCardLinksCommand { get; }

    /// <summary>Puts one workstream away from the card itself, or brings it back.</summary>
    public RelayCommand ArchiveCardCommand { get; }

    /// <summary>The editor for the card being looked at.</summary>
    public DetailDrawerViewModel Drawer { get; }

    /// <summary>The settings panel.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>The settings themselves, for the window position that is not anybody's to edit.</summary>
    public ISettingsStore SettingsStore => _settings;

    /// <summary>What the buttons do, and what to look at when one of them does nothing.</summary>
    public HelpViewModel Help { get; } = new();

    /// <summary>Grouping and placing the terminal windows on the desktop.</summary>
    public ArrangeViewModel Arrange { get; } = new();

    /// <summary>What was worked on today, written to a file and shown.</summary>
    public DayReportViewModel DayReport { get; }

    /// <summary>What a workstream's sessions read and can run.</summary>
    public InstructionsViewModel Instructions { get; }

    /// <summary>Which build is running, shown so an installed copy can be told from the last one.</summary>
    public string Version => AppVersion.Current;

    /// <summary>The same, with the build time spelled out.</summary>
    public string VersionDescribed => AppVersion.Described;

    /// <summary>The composer for a workstream that does not exist yet.</summary>
    public NewWorkstreamViewModel NewWorkstream { get; }

    /// <summary>Asks one workstream session to write a handoff and exit.</summary>
    public RelayCommand StopCardCommand { get; }

    /// <summary>Asks the ticked cards' running sessions to write a handoff and exit.</summary>
    public RelayCommand StopSelectedCommand { get; }

    /// <summary>Folds the live sessions panel away, or brings it back.</summary>
    public RelayCommand ToggleSessionsPanelCommand { get; }

    /// <summary>Whether the live sessions panel is showing. Remembered between runs.</summary>
    public bool IsSessionsPanelOpen
    {
        get => _isSessionsPanelOpen;
        set
        {
            if (Set(ref _isSessionsPanelOpen, value))
            {
                _settings.Write(_settings.Read() with { SessionsPanelCollapsed = !value });
            }
        }
    }

    /// <summary>What the last action did, shown under the title bar.</summary>
    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    /// <summary>Which theme is in force. Applied immediately and remembered for next time.</summary>
    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (!Set(ref _theme, value))
            {
                return;
            }

            ThemeApplier.Apply(value);
            _settings.Write(_settings.Read() with { Theme = value });
        }
    }

    /// <summary>
    /// Which Claude account the sessions run as. A machine signs in once, so this is the board's
    /// account rather than any one session's. Signing out and in as another account changes it
    /// under a running Theseus, so it is re-read rather than remembered from startup.
    /// </summary>
    public string Account => _account.Current.Describe;

    /// <summary>Shows a standing warning about the environment Theseus is running in.</summary>
    public void Warn(string warning) => Notice = warning;

    /// <summary>Applies the remembered theme at startup.</summary>
    public void ApplyTheme() => ThemeApplier.Apply(_theme);

    /// <summary>Re-reads the plans, the board and the live sessions.</summary>
    public void Refresh()
    {
        Timed("board", Board.Refresh);

        // A refresh replaces every card, so the new ones need telling where to report a tick. Wiring
        // this only when the grid changes left the selection count frozen at whatever it said before
        // the last refresh.
        WireLanes();
        Timed("sessions", Sessions.Refresh);
        Timed("lifecycles", SyncLifecycles);
        Timed("pull requests", SyncPullRequests);
        WireAdoption();

        // The strip is answered by what is on the board and which sessions are alive, and a refresh
        // changes both. Without this it would still name a session that had gone.
        Timed("stops", StopsChanged);
        Timed("account", () => SyncAccount());
        _stopRequests.Sweep(DateTimeOffset.UtcNow);
    }

    // Start-up has taken anything from 4 seconds to over 2 minutes on the same machine, with nothing
    // logged in between. A slow step names itself.
    private static void Timed(string step, Action action)
    {
        var watch = Stopwatch.StartNew();
        action();

        if (watch.Elapsed > SlowStep)
        {
            TheseusLog.Information("Refreshing the board: {Step} took {Seconds:0.0}s", step, watch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>
    /// Opens the composer for a session's proposal, as a follow-on to the card that proposed it. The
    /// card exists only once Nigel presses Create there.
    /// </summary>
    public void ReviewProposal(Theseus.Core.Proposals.WorkstreamProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (Board.Card(proposal.RequesterKey) is not { } parent)
        {
            Notice = $"{proposal.RequesterKey}, which proposed {proposal.Request.Title}, is no longer on the board.";

            return;
        }

        NewWorkstream.OpenProposal(parent.Workstream, proposal);
    }

    /// <summary>
    /// <para>Cheap enough to call whenever the window is looked at: two file timestamps unless something changed.</para>
    /// Notices a switch to the other account. The usage shown beside it belongs to whichever account
    /// was signed in when it was read, so it is thrown away rather than left to describe the account
    /// just left behind - which is the one reading that would send you back to the exhausted one.
    /// </summary>
    public void SyncAccount()
    {
        if (!_account.Refresh())
        {
            return;
        }

        Raise(nameof(Account));
        Notice = $"Signed in as {_account.Current.Describe}.";
        Usage?.RefreshCommand.Execute(null);
    }

    private IReadOnlyDictionary<string, string> SessionNames() =>
        Sessions.Sessions
            .GroupBy(session => session.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<StopStep> CardSteps(object? parameter) =>
        parameter is WorkstreamCardViewModel card ? StepsFor([card]) : [];

    private IReadOnlyList<StopStep> ChosenSteps() =>
        StepsFor(Chosen());

    /// <summary>
    /// Ticks or unticks every card, including the ones in the last column. Nothing is held back:
    /// hiding a card from Select all would be the same guess about the board's shape that this
    /// replaced, and a tick the user can see and remove is honest where a silent exclusion is not.
    /// </summary>
    private void ChooseAll(bool chosen)
    {
        foreach (var card in Board.Cards)
        {
            card.IsSelected = chosen;
        }

        SelectionMoved();
    }

    /// <summary>
    /// Ticks what is not ticked and unticks what is. Materialised first because the board's cards
    /// come from a query over the rows, and turning one over while walking it is asking for trouble.
    /// </summary>
    private void Invert()
    {
        foreach (var card in Board.Cards.ToList())
        {
            card.IsSelected = !card.IsSelected;
        }

        SelectionMoved();
    }

    /// <summary>
    /// Tells the buttons that act on a selection to look again. They are enabled by what is ticked,
    /// and WPF has no way of knowing that a property on a card changes what a command on the shell
    /// can do.
    /// </summary>
    private void SelectionMoved()
    {
        Raise(nameof(SelectionSummary));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Prepares a stop request and puts it in front of the user, one session at a time: the tab is
    /// brought to the front and the request goes on the clipboard, ready to paste and send.
    ///
    /// Theseus does not deliver it. A session takes its instructions from its user and refuses to
    /// let a peer override them, which is right - a peer that could say "stop and exit" could say
    /// anything - so a request carried by another session is declined however it is worded. Going
    /// through the session's own prompt makes it the user's instruction, which is what it is.
    ///
    /// Nothing is killed either way. The point of stopping is the handoff written on the way out,
    /// and a killed process writes nothing.
    /// </summary>
    /// <summary>
    /// How long a stop request stays worth delivering. Long enough to survive a session deep in a
    /// slow turn, short enough that one made this evening cannot interrupt tomorrow morning.
    /// </summary>
    private static readonly TimeSpan StopRequestLife = TimeSpan.FromHours(4);

    private static readonly TimeSpan SlowStep = TimeSpan.FromSeconds(1);

    private void BeginStop(IReadOnlyList<StopStep> steps)
    {
        _stopQueue.Clear();

        if (steps.Count == 0)
        {
            return;
        }

        var installed = HookInstallation.IsInstalled(SettingsFile());
        var asked = 0;
        var waiting = 0;
        var unreached = 0;
        var blocked = 0;
        var interrupted = 0;

        foreach (var step in steps)
        {
            var alreadyAsked = _stopRequests.Waiting(step.SessionId) is not null;

            switch (StopRouting.For(step.Lifecycle, installed, alreadyAsked))
            {
                case StopRoute.WaitForTurnEnd:
                    Ask(step);
                    asked++;
                    break;

                case StopRoute.Interrupt:
                case StopRoute.Nudge:
                    var cutIn = StopRouting.For(step.Lifecycle, installed, alreadyAsked) == StopRoute.Interrupt;

                    Ask(step);

                    var nudge = TabNudge.Nudge(step.Pid, step.SessionName, StopRouting.NudgeMarker, cutIn);

                    TheseusLog.Information(
                        cutIn
                            ? "Interrupted {Workstream}, which had been asked once and was still working: {Outcome}"
                            : "Nudged {Workstream}, which was idle and would otherwise never take another turn: {Outcome}",
                        step.WorkstreamKey, nudge);

                    switch (nudge)
                    {
                        case NudgeOutcome.Sent when cutIn:
                            interrupted++;
                            break;

                        case NudgeOutcome.Sent:
                            asked++;
                            break;

                        case NudgeOutcome.BlockedByElevation:
                            blocked++;
                            break;

                        default:
                            unreached++;
                            break;
                    }

                    break;

                case StopRoute.AnswerItFirst:
                    Ask(step);
                    TerminalFocus.FocusSession(step.Pid, step.SessionName);
                    waiting++;
                    break;

                case StopRoute.PasteYourself:
                    _stopQueue.Add(step);
                    break;
            }
        }

        if (_stopQueue.Count > 0)
        {
            AdvanceStop();

            return;
        }

        Refresh();
        Notice = Describe(asked, waiting, unreached, blocked, interrupted);
    }

    /// <summary>
    /// What was actually set in motion. A session that has been asked has not yet been told: the
    /// request waits until its next turn ends, so saying it has stopped would be ahead of the facts.
    /// </summary>
    private static string Describe(int asked, int waiting, int unreached, int blocked, int interrupted)
    {
        if (asked == 0 && unreached == 0 && blocked == 0 && interrupted == 0)
        {
            return "Nothing to stop.";
        }

        var said = new List<string>();

        if (interrupted == 1)
        {
            said.Add("One session was interrupted mid-turn and handed the request.");
        }
        else if (interrupted > 1)
        {
            said.Add($"{interrupted} sessions were interrupted mid-turn and handed the request.");
        }

        if (asked == 1)
        {
            said.Add("One session will be asked to write its handoff and exit when its turn ends.");
        }
        else if (asked > 1)
        {
            said.Add($"{asked} sessions will be asked to write their handoffs and exit as their turns end.");
        }

        if (waiting > 0)
        {
            said.Add($"{waiting} of them is waiting on you first, and is now in front.");
        }

        if (unreached > 0)
        {
            said.Add($"{unreached} idle session(s) could not be typed into, so nothing has started their "
                     + "turn. The request is written and waiting: open the tab and press Enter.");
        }

        if (blocked > 0)
        {
            said.Add($"{blocked} idle session(s) are in a terminal running as administrator, which Windows "
                     + "will not let Theseus type into. The request is written: open the tab and press Enter.");
        }

        return string.Join(" ", said);
    }

    private void Ask(StopStep step)
    {
        _stopRequests.Write(new PendingStopRequest(
            step.SessionId,
            step.WorkstreamKey,
            step.Title,
            step.Text,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.Add(StopRequestLife))
        {
            HandoffPath = step.HandoffPath,
        });

        TheseusLog.Information("Asked {Workstream} ({SessionId}) to stop", step.WorkstreamKey, step.SessionId);
    }

    private static string? SettingsFile()
    {
        try
        {
            return File.Exists(HookInstallation.SettingsPath)
                ? File.ReadAllText(HookInstallation.SettingsPath)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void AdvanceStop()
    {
        if (_stopQueue.Count == 0)
        {
            return;
        }

        var step = _stopQueue[0];
        _stopQueue.RemoveAt(0);

        if (!ClipboardText.TryCopy(step.Text))
        {
            Notice = "The stop request could not be put on the clipboard. The log has the reason.";

            return;
        }

        TerminalFocus.FocusSession(step.Pid, step.SessionName);

        Notice = _stopQueue.Count == 0
            ? $"Paste into {step.Title} and press Enter to stop it."
            : $"Paste into {step.Title} and press Enter, then Stop all again for the next {_stopQueue.Count}.";
    }

    private IReadOnlyList<StopStep> StepsFor(IEnumerable<WorkstreamCardViewModel> cards)
    {
        var prompt = _settings.Read().StopPrompt;
        var names = SessionNames();

        return
        [
            .. cards.SelectMany(card => StopSelection
                .ForWorkstream(card.Workstream, Sessions.Snapshot, names)
                .Where(_ => Sessions.Find(card.Workstream.LastSessionId) is not null)
                .Select(target => new StopStep(
                    card.Title,
                    card.Key,
                    card.Workstream.LastSessionId!,
                    target.SessionName,
                    Sessions.Find(card.Workstream.LastSessionId)!.Pid,
                    card.Lifecycle,
                    StopRequest.For(prompt, target),
                    target.HandoffPath)))
        ];
    }

    /// <summary>
    /// What a session is doing, in one line. A workflow under way outranks the todo list: it is the
    /// larger fact about a session, and a session driving thirty agents commonly keeps no list at all.
    /// </summary>
    private string Doing(string? sessionId)
    {
        if (_activity.Read(sessionId) is { } workflow && workflow.IsRunning(DateTimeOffset.UtcNow))
        {
            return workflow.Describe;
        }

        return TodoProgress.Of(_todos.Read(sessionId)).Summary;
    }

    /// <summary>Finds the pull requests each workstream mentions and where they live.</summary>
    public void SyncPullRequests()
    {
        foreach (var card in Board.Cards)
        {
            card.PullRequests = _pullRequests.Resolve(card.Workstream);
        }
    }

    /// <summary>
    /// Whether cards may move. A board that is not being looked at should not keep the render thread
    /// awake, and a machine where animation has been switched off system-wide should be obeyed.
    /// </summary>
    public bool IsMotionAllowed
    {
        get => _motionAllowed;
        set
        {
            _motionAllowed = value;

            foreach (var card in Board.Cards)
            {
                card.MotionAllowed = value;
            }
        }
    }

    /// <summary>Brings every card's state into line with what is running and what ran today.</summary>
    public void SyncLifecycles()
    {
        var now = DateTimeOffset.Now;
        var runs = _endings.Reconcile(_runs, Sessions.Snapshot.Live, DateTimeOffset.UtcNow);
        var preferred = _settings.Read().DefaultStartMode;
        var lastStage = Board.Stages.LastOrDefault();

        foreach (var card in Board.Cards)
        {
            var session = Sessions.Find(card.Workstream.LastSessionId);
            var live = session is not null;
            var state = CardLifecycle.For(card.Key, session?.Status, live, runs, now);

            card.MotionAllowed = _motionAllowed;
            card.Lifecycle = state.Lifecycle;
            card.LifecycleSinceUtc = state.Since;
            card.LastExitedUtc = state.LastExitedUtc;
            card.LastStartedUtc = state.LastStartedUtc;
            card.IsInLastStage = lastStage is not null && string.Equals(card.Column, lastStage, StringComparison.OrdinalIgnoreCase);
            card.LastRunCrashed = !live && (state.Lifecycle == SessionLifecycle.Ghost
                                            || runs.Where(run => string.Equals(run.WorkstreamKey, card.Key, StringComparison.OrdinalIgnoreCase))
                                                .MaxBy(run => run.StartedUtc)?.ExitReason == SessionExitReason.CrashedOrKilled);
            card.TodoSummary = live ? Doing(card.Workstream.LastSessionId) : string.Empty;

            // A request that has been written but not yet collected is otherwise invisible, and the
            // wait can be several minutes on a long turn. Without this the board looks identical
            // whether Stop was pressed or not, which reads as the button having done nothing.
            card.StopPending = live
                               && card.Workstream.LastSessionId is { } pendingFor
                               && _stopRequests.Waiting(pendingFor) is not null;
            card.RunningAs = live ? RunningAs.For(card.Title, session?.Name) : string.Empty;

            // Only for a card that is offering Restart. Measuring a transcript is a read of a file
            // that runs to tens of megabytes, and there is nothing to say about a session that is
            // already running.
            var measured = live ? default : TranscriptTail.Measure(card.Workstream.LastSessionId);

            card.CanResume = measured.TotalBytes > 0;
            card.ResumeTooltip = Theseus.Core.Domain.RestartCost.Tooltip(measured.TailBytes, measured.TotalBytes);
            card.RestartCost = live
                ? string.Empty
                : StartModeChoice.Tooltip(preferred, card.CanResume, card.ResumeTooltip);

            // The other way to start, whichever the board prefers. Named explicitly in the log: the
            // caller's own name here would be this method's, which says nothing about the gesture.
            card.ResumeCommand ??= new RelayCommand(
                _ => StartMany([card.Workstream], startMode: StartMode.Resume, trigger: "ResumeCard"),
                _ => CanStartCard(card) && card.CanResume);
            card.StartFreshCommand ??= new RelayCommand(
                _ => StartMany([card.Workstream], startMode: StartMode.Fresh, trigger: "StartFreshCard"),
                _ => CanStartCard(card));

            // The card's menu carries the flags and Spawn follow-on, so a visit to the details is not
            // needed for them. A context menu cannot reach the window's commands, so these are the card's.
            card.ToggleBlockedCommand ??= new RelayCommand(_ => SetFlags(card,
                card.Workstream with { IsBlocked = !card.Workstream.IsBlocked }));
            card.ToggleReopenTomorrowCommand ??= new RelayCommand(_ => SetFlags(card,
                card.Workstream with { ReopenTomorrow = !card.Workstream.ReopenTomorrow }));
            card.SpawnFollowOnCommand ??= new RelayCommand(
                _ => SpawnFollowOnCommand.Execute(card),
                _ => SpawnFollowOnCommand.CanExecute(card));

            // Read for every card with a transcript, live or not: which model a stopped session was
            // on is worth knowing before restarting it. Cheap when nothing has changed - the reader
            // remembers its answer against the file's length - and a tail read otherwise.
            card.Model = TranscriptModel.Read(card.Workstream.LastSessionId);
        }

        Chains?.Update();

        var handoffs = Board.Cards.ToDictionary(card => card, card => HandoffPath.For(card.Workstream));

        SyncClashes(handoffs);
        PublishSnapshot(handoffs);
        CloseFinishedSessions();
    }

    private void PublishSnapshot(Dictionary<WorkstreamCardViewModel, string> handoffs) =>
        Snapshots?.Publish(new BoardSnapshot(
            [.. handoffs.Keys.Select(card => new BoardSnapshotCard(card.Workstream, handoffs[card],
                card.Lifecycle, card.IsLive, card.IsInLastStage, card.LastStartedUtc, card.LastRunCrashed))],
            DateTimeOffset.UtcNow));

    /// <summary>
    /// Types /exit into any session that was asked to stop, has written its handover, and has gone
    /// quiet — the last step of stopping, which no session can take for itself.
    ///
    /// Nothing is killed. A killed process writes nothing, which is the whole reason stopping exists;
    /// this types what a person would type, and the session exits the ordinary way, which lets the
    /// launcher finish and the tab close on its own. The typing refuses unless focus is in a terminal
    /// buffer, so a keystroke cannot land somewhere unintended.
    /// </summary>
    private void CloseFinishedSessions()
    {
        foreach (var card in Board.Cards)
        {
            if (card.Workstream.LastSessionId is not { } sessionId
                || Sessions.Find(sessionId) is not { } session
                || _unconfirmed.Contains(sessionId))
            {
                continue;
            }

            var delivered = _stopRequests.Delivered(sessionId);

            if (delivered is not null && SessionClosing.StartedSince(delivered, session.StartedUtc))
            {
                _stopRequests.Retire(sessionId);
                TheseusLog.Information(
                    "Retired the stop request for {Workstream}: the session running now started after it was asked, so it is not the one that was asked",
                    card.Key);
                continue;
            }

            if (!SessionClosing.ShouldClose(delivered, WrittenUtc(delivered?.HandoffPath), session.Status,
                    session.StatusSinceUtc, DateTimeOffset.UtcNow, _closed.Contains(sessionId)))
            {
                continue;
            }

            _closed.Add(sessionId);

            var outcome = TabNudge.Nudge(session.Pid, session.Name, SessionClosing.ExitCommand);

            // The request is finished with the moment the exit is typed, and is taken off disk here
            // rather than left to expire. `_closed` above only stops a second attempt inside this
            // process; it is empty again in the next one, so a request left lying about was acted on
            // by every Theseus that started for the next four hours. On 2026-09-02 that closed this
            // very session twice, hours apart, the second time long after it had been resumed.
            //
            // A request whose keystrokes never landed is deliberately left alone: nothing was told to
            // exit, so there is still something to do, and Sweep expires it if that never happens.
            if (outcome == NudgeOutcome.Sent)
            {
                _stopRequests.Retire(sessionId);

                // The one ending Theseus can be certain of, recorded at the moment it is caused.
                // Nothing else was recording it: the reconcile only writes an ending when a session
                // it watched vanishes, and files it as a crash. Of 168 endings in the log, 134 said
                // CrashedOrKilled and 11 ClosedCleanly, and the six sessions stopped on the evening
                // of 2026-09-03 had no ending at all - which is what let the day report credit a
                // resumed session with the days it had been dead.
                if (_runs.Read()
                        .Where(run => run.EndedUtc is null
                                      && string.Equals(run.ClaudeSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                        .MaxBy(run => run.StartedUtc) is { } open)
                {
                    _runs.AppendEnd(new RunEnd(open.Id, DateTimeOffset.UtcNow, null, SessionExitReason.ClosedCleanly));
                }
            }

            TheseusLog.Information(
                "Closing {Workstream}, which was asked to stop and has handed over: {Outcome}",
                card.Key, outcome);
        }
    }

    /// <summary>
    /// When the handover document a stop request named was last written, or null if it is not there.
    /// The path comes from the request rather than being worked out again, so what is checked is the
    /// file the session was actually told to write.
    /// </summary>
    private static DateTimeOffset? WrittenUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Tells each card what it shares with another that it should not.
    ///
    /// The handover document is the one that matters. Two workstreams resolving to one file is
    /// silent, and it has already happened here: both sessions wrote to it, and the only reason
    /// nothing was lost is that one of them noticed the file changing underneath it. Neither card
    /// could see past itself, and the board — which can see all of them — was not looking.
    /// </summary>
    private void ArchiveCard(object? parameter)
    {
        if (parameter is not WorkstreamCardViewModel card)
        {
            return;
        }

        var archived = !card.IsArchived;
        var title = card.Title;

        Board.SetArchived(card, archived);
        Refresh();

        Notice = archived
            ? $"Archived {title}. It is under Archived until you restore it."
            : $"Restored {title}.";
    }

    private void SyncClashes(Dictionary<WorkstreamCardViewModel, string> handoffs)
    {
        var cards = handoffs.Keys.ToList();

        var clashes = WorkstreamOverlap.Find(
            [.. cards.Select(card => (card.Key, (string?)handoffs[card], card.JiraKeys))],
            Chains?.LinkedPairs);

        foreach (var card in cards)
        {
            card.Clash = WorkstreamOverlap.Describe(card.Key, clashes);
            card.LinkedShare = WorkstreamOverlap.DescribeLinked(card.Key, clashes);
        }
    }

    /// <summary>
    /// The cards the user has ticked. This is the only thing a bulk Start or Stop looks at: there is
    /// no rule about which column is safe, because there is nothing left to guess.
    /// </summary>
    /// <summary>
    /// The cards whose unfinished stop is waiting on an answer, and whose session is still there to
    /// act on. A request for a session that has since gone is not worth asking about.
    /// </summary>
    private IReadOnlyList<WorkstreamCardViewModel> UnconfirmedStops() =>
    [
        .. Board.Cards.Where(card =>
            card.Workstream.LastSessionId is { } id
            && _unconfirmed.Contains(id)
            && Sessions.Find(id) is not null)
    ];

    /// <summary>
    /// Folds the board down to what is alive, or opens all of it again.
    ///
    /// Collapsing sets every card at once rather than only the inactive ones, so a live card that was
    /// folded before it started is opened by the same press. The board then says exactly one thing:
    /// folded means nothing is running there.
    /// </summary>
    private void Collapse(bool inactiveOnly)
    {
        foreach (var card in Board.Cards)
        {
            card.IsCollapsed = inactiveOnly && !card.IsLive;
        }

        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Lets the held-back stops run their course, which closes those sessions.</summary>
    private void ConfirmStops()
    {
        var count = UnconfirmedStops().Count;

        _unconfirmed.Clear();
        StopsChanged();
        Notice = count == 1 ? "Closing the session that was asked to stop." : $"Closing {count} sessions.";
        CloseFinishedSessions();
    }

    /// <summary>
    /// Drops the held-back stops. The requests go from disk as well, or the same question would be
    /// asked again on the next launch and the answer would count for nothing.
    /// </summary>
    private void AbandonStops()
    {
        var count = UnconfirmedStops().Count;

        foreach (var sessionId in _unconfirmed)
        {
            _stopRequests.Retire(sessionId);
        }

        _unconfirmed.Clear();
        StopsChanged();
        Notice = count == 1 ? "Left it running." : $"Left {count} sessions running.";
    }

    private void StopsChanged()
    {
        Raise(nameof(HasUnconfirmedStops));
        Raise(nameof(UnconfirmedStopsSummary));
    }

    private void InputsChanged()
    {
        Raise(nameof(HasHeldForInputs));
        Raise(nameof(HeldForInputsSummary));
    }

    private void TabsChanged()
    {
        Raise(nameof(HasHeldForTabs));
        Raise(nameof(HeldForTabsSummary));
    }

    /// <summary>
    /// Which of these already have a tab of their own name with nothing running in it.
    ///
    /// Reading every terminal's tab titles is a UI Automation walk, measured at 120–340ms on Nigel's
    /// machine — cheap enough for a Start press, and paid once for the whole batch rather than per
    /// workstream. A terminal running elevated cannot be read at all, so this finds what it can.
    /// </summary>
    private IReadOnlyList<Workstream> Abandoned(IReadOnlyList<Workstream> workstreams)
    {
        try
        {
            var titles = TerminalWindows.All()
                .SelectMany(window => window.Tabs)
                .ToList();

            return AbandonedTabs.Among(workstreams, titles, Sessions.Snapshot);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            TheseusLog.Debug("The terminal's tabs could not be read before starting: {Reason}", exception.Message);

            return [];
        }
    }

    private IReadOnlyList<WorkstreamCardViewModel> Chosen() =>
        [.. Board.Cards.Where(card => card.IsSelected)];

    private IReadOnlyList<Workstream> ChosenToStart() =>
        StartSelection.ForCards(
            [.. Chosen().Select(card => card.Workstream)], [.. Board.Stages], Sessions.Snapshot);

    private IReadOnlyList<Workstream> Workstreams() =>
        [.. Board.Cards.Select(card => card.Workstream)];

    private bool CanStartCard(object? parameter) =>
        parameter is WorkstreamCardViewModel card
        && _launcher.CanLaunch
        && StartSelection.IsEligible(card.Workstream, Sessions.Snapshot);

    private void StartCard(object? parameter)
    {
        if (parameter is WorkstreamCardViewModel card)
        {
            StartMany([card.Workstream]);
        }
    }

    /// <summary>
    /// Starts what was asked for, holding back anything that already has a tab of its own name with
    /// nothing running in it. Those are asked about rather than launched: opening a second tab beside
    /// an abandoned one is what surprised Nigel on 2026-09-07, and the board gave no sign which tab
    /// was which.
    /// </summary>
    private void StartMany(IReadOnlyList<Workstream> workstreams, bool evenIfTabsExist = false,
        StartMode? startMode = null, bool evenIfWaiting = false,
        [System.Runtime.CompilerServices.CallerMemberName] string trigger = "")
    {
        // Which gesture asked, recorded before anything can go wrong with it. There are four callers
        // — a card's own button, Start selected, "Open new tabs", and creating a workstream — and
        // when one press produced two launches on 2026-09-08 nothing said whether one caller had run
        // twice or two callers had each run once. Those want different fixes.
        TheseusLog.Information("{Trigger} asked to start {Count} workstream(s) ({StartMode}): {Keys}",
            trigger, workstreams.Count, startMode?.ToString() ?? "board default", string.Join(", ", workstreams.Select(workstream => workstream.Key)));

        if (_launcher.Impediment is { } impediment)
        {
            Notice = impediment;

            return;
        }

        // Asked about before anything else: a card still waiting on an input has nothing to read, and
        // is asked about whether one card was pressed or a whole selection.
        if (!evenIfWaiting && Chains?.Waiting(workstreams) is { Count: > 0 } waiting)
        {
            _heldForInputs = waiting;
            _heldForInputsMode = startMode;
            InputsChanged();

            workstreams = [.. workstreams.Except(waiting)];

            if (workstreams.Count == 0)
            {
                return;
            }
        }

        if (!evenIfTabsExist && Abandoned(workstreams) is { Count: > 0 } held)
        {
            // The rest go now; only the ones with a tab already open wait for an answer.
            _heldForTabs = held;
            _heldStartMode = startMode;
            TabsChanged();

            workstreams = [.. workstreams.Except(held)];

            if (workstreams.Count == 0)
            {
                return;
            }
        }

        var started = new List<string>();
        var refusals = new List<string>();

        foreach (var candidate in workstreams)
        {
            var workstream = Board.Pin(candidate);

            if (_launcher.Launch(workstream, _plans.PathFor(workstream.Key), startMode) is not { } sessionId)
            {
                if (_launcher.LastRefusal is { } refusal)
                {
                    refusals.Add(refusal);
                }

                continue;
            }

            // Starting a workstream withdraws any request to stop it. A resumed session keeps its
            // identifier, so a request nobody collected outlives the session it was meant for and is
            // handed to the next one on its opening prompt - told to write a handoff and exit before
            // it has done anything at all.
            if (_stopRequests.Cancel(sessionId))
            {
                TheseusLog.Information(
                    "Withdrew the stop request for {Workstream}, which is being started again", workstream.Key);
            }

            // A request already handed over is finished with too. On 2026-10-07 nexus-admin was asked
            // to stop, handed over, and was never closed; Nigel restarted it, and because the resumed
            // session kept the same identifier, the delivered request was still on disk. Two minutes
            // later the close step found it idle with a fresh handoff and typed /exit into the new one.
            if (_stopRequests.Retire(sessionId))
            {
                TheseusLog.Information(
                    "Retired the delivered stop request for {Workstream}, which is being started again", workstream.Key);
            }

            Remember(workstream, sessionId);
            started.Add(workstream.Title);
        }

        Notice = StartNotice.For(started, refusals);

        Refresh();
    }

    private void Remember(Workstream workstream, string sessionId)
    {
        Board.RememberSession(workstream.Key, sessionId);

        if (_plans.TryRead(workstream.Key) is { } plan)
        {
            _plans.Write(workstream.Key, plan with { ResumeSessionId = sessionId });
        }
    }

    /// <summary>
    /// Offers every workstream as an adoption target on every live session. Building the commands
    /// here means a target already knows both halves of the pairing, so the menu item is one click
    /// with nothing selected beforehand.
    /// </summary>
    /// <summary>
    /// Offers back a session running for a workstream the board stopped following. Restarting a
    /// session outside Theseus gives it a new identifier, and the card then reads as finished
    /// indefinitely — the one state a card can be plainly wrong in. Nothing is reattached without
    /// being asked: the offer is made on the card and taken by a click.
    /// </summary>
    private void OfferLostSessions(IReadOnlyList<WorkstreamCardViewModel> cards)
    {
        var lost = LostSessions.Find(Workstreams(), Sessions.Snapshot.Live)
            .ToDictionary(entry => entry.WorkstreamKey, StringComparer.OrdinalIgnoreCase);

        foreach (var card in cards)
        {
            if (!lost.TryGetValue(card.Key, out var found))
            {
                card.LostSessionName = string.Empty;
                card.AdoptLostCommand = null;

                continue;
            }

            var target = card;

            card.LostSessionName = found.SessionName;
            card.AdoptLostCommand = new RelayCommand(_ =>
            {
                if (Sessions.Find(found.SessionId) is { } session)
                {
                    Adopt(session, target);
                }
            });
        }
    }

    private void WireAdoption()
    {
        var cards = Board.Cards.ToList();

        OfferLostSessions(cards);

        foreach (var session in Sessions.Sessions)
        {
            session.AdoptTargets.Clear();
            session.IsAdopted = cards.Any(card => string.Equals(
                card.Workstream.LastSessionId, session.SessionId, StringComparison.OrdinalIgnoreCase));

            var running = session;

            session.AdoptTargets.Add(new AdoptTarget(
                "New workstream from this session",
                new RelayCommand(_ => CreateFrom(running))));

            foreach (var card in cards)
            {
                var target = card;

                session.AdoptTargets.Add(new AdoptTarget(
                    card.Title,
                    new RelayCommand(_ => Adopt(running, target))));
            }
        }
    }

    /// <summary>
    /// Creates a workstream around a session that is already running, and adopts it. The colour is
    /// allocated rather than chosen: it is the first colour this application has ever picked, since
    /// every workstream so far arrived with one from its plan file.
    /// </summary>
    private void CreateFrom(LiveSessionViewModel session)
    {
        var taken = _plans.Keys();
        var key = WorkstreamKey.Unique(session.Name, taken);

        var inUse = taken
            .Select(existing => _plans.TryRead(existing)?.Color)
            .Where(colour => !string.IsNullOrWhiteSpace(colour))
            .Select(colour => colour!)
            .ToList();

        var colour = _colours.Allocate(new ColourAllocationState(inUse, []));

        var order = taken.Count + 1;
        var plan = new SessionPlan(order, session.Name, colour, session.WorkingDirectory, [],
            session.SessionId, DateTimeOffset.Now.ToString("s"),
            $"Continue the {session.Name} work. Read the session's own notes before changing anything.");

        _plans.Write(key, plan);

        Refresh();

        Notice = $"Created {session.Name} and adopted the running session.";
    }

    private void Adopt(LiveSessionViewModel session, WorkstreamCardViewModel card)
    {
        Remember(card.Workstream, session.SessionId);
        Notice = $"{card.Title} now follows {session.Name} (pid {session.Pid}).";
        Refresh();
    }

    private void ShowCard(object? parameter) => ShowCard(parameter, DrawerTab.Overview);

    private void ShowCard(object? parameter, DrawerTab tab)
    {
        if (parameter is not WorkstreamCardViewModel card)
        {
            return;
        }

        Board.Select(card);
        Drawer.Show(card, tab);

        if (ReferenceEquals(Drawer.Card, card))
        {
            Chains?.ShowFor(card.Key);
        }
    }

    private void SetFlags(WorkstreamCardViewModel card, Workstream flagged)
    {
        card.Workstream = flagged;
        Board.Persist(card);
        Drawer.Reload(card);
        Refresh();
    }

    private bool CanFocusCard(object? parameter) =>
        parameter is WorkstreamCardViewModel card && Sessions.Find(card.Workstream.LastSessionId) is not null;

    private void FocusCard(object? parameter)
    {
        if (parameter is not WorkstreamCardViewModel card
            || Sessions.Find(card.Workstream.LastSessionId) is not { } session)
        {
            return;
        }

        Notice = Describe(TerminalFocus.FocusSession(session.Pid, session.Name), card.Title);
    }

    /// <summary>
    /// Says what the focus actually achieved. Landing on the window but not the tab is the normal
    /// outcome for a session Theseus did not launch, and saying so beats leaving the operator to
    /// wonder why the wrong tab is showing.
    /// </summary>
    private static string Describe(FocusOutcome outcome, string title) => outcome switch
    {
        FocusOutcome.TabSelected => $"Focused {title}.",
        FocusOutcome.WindowOnly =>
            $"Found the terminal, but not which tab {title} is in — only sessions Theseus started can be pinpointed.",
        _ => $"No terminal window found for {title}.",
    };

    private static void OpenPullRequest(object? parameter)
    {
        if (parameter is PullRequestLink { Url: { } url })
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void OpenJira(object? parameter)
    {
        if (parameter is not string key)
        {
            return;
        }

        if (JiraSite.IssueUrl(_settings.Read().JiraBaseUrl, key) is { } url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

            return;
        }

        Notice = string.IsNullOrWhiteSpace(_settings.Read().JiraBaseUrl)
            ? $"Set your Jira address in Settings to open {key}."
            : $"{key} does not look like an issue in the configured Jira.";
    }
}
