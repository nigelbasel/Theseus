using System.Diagnostics;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Colour;
using Theseus.Core.Launching;
using Theseus.Core.Sessions;
using Theseus.Core.Settings;
using Theseus.Core.Stopping;

namespace Theseus.App.ViewModels;

/// <summary>The window's own state: what it shows, what it can start, and how it is painted.</summary>
public sealed class ShellViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly LaunchCoordinator _launcher;
    private readonly IPlanStore _plans;
    private readonly ITabColourAllocator _colours;
    private readonly PullRequestResolver _pullRequests;
    private readonly SessionStopper _stopper;
    private readonly IRunLog _runs;
    private readonly ISessionTodoReader _todos;
    private AppTheme _theme;
    private string _notice = string.Empty;
    private bool _isSessionsPanelOpen;

    /// <summary>Creates the shell over everything the window drives.</summary>
    public ShellViewModel(
        BoardViewModel board,
        SessionsViewModel sessions,
        ISettingsStore settings,
        LaunchCoordinator launcher,
        IPlanStore plans,
        IRunLog runs,
        ISessionTodoReader todos,
        ITabColourAllocator colours)
    {
        Board = board;
        _runs = runs;
        _todos = todos;
        Sessions = sessions;
        _settings = settings;
        _launcher = launcher;
        _plans = plans;
        _colours = colours;
        _pullRequests = new PullRequestResolver(new GitRemoteReader());
        _stopper = new SessionStopper(new ClaudeLocator(settings.Read().ClaudeExecutablePath));
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
        StopCardCommand = new RelayCommand(parameter => _ = StopAsync(CardTargets(parameter)),
            parameter => CardTargets(parameter).Count > 0);
        StopBoardCommand = new RelayCommand(_ => _ = StopAsync(BoardTargets()), _ => BoardTargets().Count > 0);

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
    }

    private IReadOnlyDictionary<string, string> SessionNames() =>
        Sessions.Sessions
            .GroupBy(session => session.SessionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<StopTarget> CardTargets(object? parameter) =>
        parameter is WorkstreamCardViewModel card
            ? StopSelection.ForWorkstream(card.Workstream, Sessions.Snapshot, SessionNames())
            : [];

    private IReadOnlyList<StopTarget> BoardTargets() =>
        StopSelection.ForBoard(Workstreams(), Sessions.Snapshot, SessionNames());

    /// <summary>
    /// Asks the named sessions to write a handoff and exit. Nothing is killed: a session that will
    /// not stop is reported as still running, because the point of stopping is the handoff it
    /// writes on the way out and a killed process writes nothing.
    /// </summary>
    private async Task StopAsync(IReadOnlyList<StopTarget> targets)
    {
        if (targets.Count == 0)
        {
            return;
        }

        Notice = targets.Count == 1
            ? $"Asking {targets[0].SessionName} to write a handoff and exit..."
            : $"Asking {targets.Count} sessions to write a handoff and exit...";

        var asked = await _stopper.RequestAsync(_settings.Read().StopPrompt, targets, CancellationToken.None);

        if (asked is null)
        {
            Notice = "The stop request could not be sent. The log has the reason.";

            return;
        }

        Notice = asked == 1
            ? $"Asked {targets[0].SessionName} to stop. It will exit once it has written its handoff."
            : $"Asked {asked} sessions to stop. They will exit once they have written their handoffs.";

        Refresh();
    }

    /// <summary>Finds the pull requests each workstream mentions and where they live.</summary>
    public void SyncPullRequests()
    {
        foreach (var card in Board.Columns.SelectMany(column => column.Cards))
        {
            card.PullRequests = _pullRequests.Resolve(card.Workstream);
        }
    }

    /// <summary>Brings every card's state into line with what is running and what ran today.</summary>
    public void SyncLifecycles()
    {
        var runs = _runs.Read();
        var now = DateTimeOffset.Now;

        foreach (var card in Board.Columns.SelectMany(column => column.Cards))
        {
            var live = Sessions.Find(card.Workstream.LastSessionId) is not null;
            var state = CardLifecycle.For(card.Key, live, runs, now);

            card.Lifecycle = state.Lifecycle;
            card.LifecycleSinceUtc = state.Since;
            card.TodoSummary = live
                ? TodoProgress.Of(_todos.Read(card.Workstream.LastSessionId)).Summary
                : string.Empty;
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
    private void WireAdoption()
    {
        var cards = Board.Columns.SelectMany(column => column.Cards).ToList();

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
