using System.Diagnostics;
using System.IO;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
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
        StartBoardCommand = new RelayCommand(_ => StartMany(BoardSelection()), _ => BoardSelection().Count > 0);
        SelectCardCommand = new RelayCommand(ShowCard);
        ToggleSessionsPanelCommand = new RelayCommand(_ => IsSessionsPanelOpen = !IsSessionsPanelOpen);
        StopCardCommand = new RelayCommand(parameter => BeginStop(CardSteps(parameter)),
            parameter => CardSteps(parameter).Count > 0);
        StopBoardCommand = new RelayCommand(_ => BeginStop(BoardSteps()), _ => BoardSteps().Count > 0);

        Drawer = new DetailDrawerViewModel(plans, runs, new WorkflowRunReader(WorkflowRunReader.DefaultDirectory), card =>
        {
            Board.Persist(card);
            Refresh();
        });

        Settings = new SettingsViewModel(settings, Refresh);

        NewWorkstream = new NewWorkstreamViewModel(plans, colours, (key, startNow) =>
        {
            Refresh();

            if (Board.Card(key) is not { } card)
            {
                Notice = "The workstream was created but is not on the board yet.";

                return;
            }

            if (startNow)
            {
                StartMany([card.Workstream]);

                return;
            }

            Notice = $"Created {card.Title}.";
        });

        WireLanes();
        Board.LanesChanged += (_, _) => WireLanes();
    }

    private void WireLanes()
    {
        foreach (var column in Board.Columns)
        {
            var captured = column;
            captured.StartAllCommand = new RelayCommand(
                _ => StartMany(ColumnSelection(captured.Column)),
                _ => ColumnSelection(captured.Column).Count > 0);
        }
    }

    /// <summary>The board this window shows.</summary>
    public BoardViewModel Board { get; }

    /// <summary>The sessions running right now.</summary>
    public SessionsViewModel Sessions { get; }

    /// <summary>The account usage shown in the status bar.</summary>
    public UsageViewModel? Usage { get; set; }

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

    /// <summary>Starts every lane except the last, which is where finished work lives.</summary>
    public RelayCommand StartBoardCommand { get; }

    /// <summary>Opens a card in the detail drawer.</summary>
    public RelayCommand SelectCardCommand { get; }

    /// <summary>The editor for the card being looked at.</summary>
    public DetailDrawerViewModel Drawer { get; }

    /// <summary>The settings panel.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>The composer for a workstream that does not exist yet.</summary>
    public NewWorkstreamViewModel NewWorkstream { get; }

    /// <summary>Asks one workstream session to write a handoff and exit.</summary>
    public RelayCommand StopCardCommand { get; }

    /// <summary>Asks every running workstream session to write a handoff and exit.</summary>
    public RelayCommand StopBoardCommand { get; }

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
        Board.Refresh();
        Sessions.Refresh();
        SyncLifecycles();
        SyncPullRequests();
        WireAdoption();
        SyncAccount();
        _stopRequests.Sweep(DateTimeOffset.UtcNow);
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

    private IReadOnlyList<StopStep> BoardSteps() =>
        StepsFor(Board.Columns.SelectMany(column => column.Cards));

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

        foreach (var step in steps)
        {
            switch (StopRouting.For(step.Lifecycle, installed))
            {
                case StopRoute.WaitForTurnEnd:
                    Ask(step);
                    asked++;
                    break;

                case StopRoute.Nudge:
                    Ask(step);

                    switch (TabNudge.Nudge(step.Pid, step.SessionName, StopRouting.NudgeMarker))
                    {
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

        Notice = Describe(asked, waiting, unreached, blocked);
    }

    /// <summary>
    /// What was actually set in motion. A session that has been asked has not yet been told: the
    /// request waits until its next turn ends, so saying it has stopped would be ahead of the facts.
    /// </summary>
    private static string Describe(int asked, int waiting, int unreached, int blocked)
    {
        if (asked == 0 && unreached == 0 && blocked == 0)
        {
            return "Nothing to stop.";
        }

        var said = new List<string>();

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

    private void Ask(StopStep step) =>
        _stopRequests.Write(new PendingStopRequest(
            step.SessionId,
            step.WorkstreamKey,
            step.Title,
            step.Text,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.Add(StopRequestLife)));

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
                    StopRequest.For(prompt, target))))
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
        foreach (var card in Board.Columns.SelectMany(column => column.Cards))
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

            foreach (var card in Board.Columns.SelectMany(column => column.Cards))
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

        foreach (var card in Board.Columns.SelectMany(column => column.Cards))
        {
            var session = Sessions.Find(card.Workstream.LastSessionId);
            var live = session is not null;
            var state = CardLifecycle.For(card.Key, session?.Status, live, runs, now);

            card.MotionAllowed = _motionAllowed;
            card.Lifecycle = state.Lifecycle;
            card.LifecycleSinceUtc = state.Since;
            card.TodoSummary = live ? Doing(card.Workstream.LastSessionId) : string.Empty;
        }
    }

    private IReadOnlyList<Workstream> BoardSelection() =>
        StartSelection.ForBoard(Workstreams(), [.. Board.Columns.Select(column => column.Column)],
            Sessions.Snapshot);

    private IReadOnlyList<Workstream> ColumnSelection(string column) =>
        StartSelection.ForColumn(Workstreams(), column, Sessions.Snapshot);

    private IReadOnlyList<Workstream> Workstreams() =>
        [.. Board.Columns.SelectMany(column => column.Cards).Select(card => card.Workstream)];

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

    private void StartMany(IReadOnlyList<Workstream> workstreams)
    {
        if (_launcher.Impediment is { } impediment)
        {
            Notice = impediment;

            return;
        }

        var started = 0;

        foreach (var workstream in workstreams)
        {
            if (_launcher.Launch(workstream) is not { } sessionId)
            {
                continue;
            }

            Remember(workstream, sessionId);
            started++;
        }

        Notice = started switch
        {
            0 => "Nothing was started.",
            1 => $"Started {workstreams[0].Title}.",
            _ => $"Started {started} sessions.",
        };

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
        var cards = Board.Columns.SelectMany(column => column.Cards).ToList();

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

    private void ShowCard(object? parameter)
    {
        if (parameter is not WorkstreamCardViewModel card)
        {
            return;
        }

        Board.Select(card);
        Drawer.Show(card);
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
