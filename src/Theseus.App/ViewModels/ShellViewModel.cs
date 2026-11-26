using Theseus.Core.Duties;
using Theseus.Core.Mcp;
using Theseus.Core.Persistence;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using Theseus.App.Services;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Colour;
using Theseus.Core.Launching;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Sessions;
using Theseus.Core.Running;
using Theseus.Core.Settings;
using Theseus.Core.Stopping;
using Theseus.Core.Terminal;
using Theseus.Core.Usage;

namespace Theseus.App.ViewModels;

/// <summary>The window's own state: what it shows, what it can start, and how it is painted.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ISettingsStore _settings;
    private readonly HoldoutChecks _holdouts = new();

    // Why a verifier Theseus started failed to start, by card, until it starts (Nigel, 2026-11-23).
    private readonly Dictionary<string, string> _verifierStartFailures = new(StringComparer.OrdinalIgnoreCase);
    private string _updaterStates = string.Empty;
    private readonly CardEventLog _cardEvents = new(Path.Combine(App.DataDirectory, "card-events.jsonl"));
    private IReadOnlyDictionary<string, CardEvent> _handIns = new Dictionary<string, CardEvent>();
    private IReadOnlyDictionary<string, DateTimeOffset> _runStarts = new Dictionary<string, DateTimeOffset>();
    private readonly ReportsOpened _reportsOpened = new(Path.Combine(App.DataDirectory, "reports-opened.json"));
    private IReadOnlyDictionary<string, ProjectBranchGrant> _branchGrants = new Dictionary<string, ProjectBranchGrant>();
    private readonly BackgroundProjectReads _projectReads;
    private readonly ProjectFolderWatcher _projectWatcher;
    private bool _hasProjectsFolder;
    private readonly SpecAcceptanceStore _acceptances;
    private readonly ReportedAcceptanceStore _reported;
    private readonly OwnerEventLog _ownerEvents = new(Path.Combine(App.DataDirectory, "owner-events.jsonl"));

    /// <summary>Sessions' requests to start cards the owner paused (sessions-run-the-project items 12 to 15).</summary>
    public StartRequestBook StartRequests { get; } =
        new(new StartRequestStore(Path.Combine(App.DataDirectory, "start-requests.json")), new Theseus.Core.Abstractions.SystemClock());
    private SessionTells? _sessionTells;

    private SessionTells Tells => _sessionTells ??= new SessionTells(new StopRequestStore(StopRequestRoot.Tells), _ownerEvents);
    private IReadOnlyList<SessionRun>? _refreshRuns;
    private string? _lastOpenedProject;
    private string? _openProjectName;
    private ProjectViewModel? _projectView;

    // Set when an edit was refused because the view is older than the file, so the next wiring draws it again even
    // though its Signature, which holds no line numbers, has not changed (1.1d PR 4 review, finding 1).
    private bool _redrawProjectView;
    private ProjectViewActions? _projectActions;
    private ProjectRunner? _runner;
    private CardRunCommands? _runCommands;
    private IWorkstreamControl? _workstreamControl;
    private bool _projectStartSaid;
    private readonly ProjectViewHost _viewHost;
    private IReadOnlyList<ProjectChoice> _openProjectChoices = [];
    private IReadOnlyList<ProjectChoice> _openSpecChoices = [];
    private IReadOnlyList<ProjectChoice> _acceptSpecChoices = [];
    private Theseus.Core.Terminal.ProjectWindowTarget _projectWindow = new(null, "Open a project first");
    private string _projectSummary = string.Empty;
    private readonly LaunchCoordinator _launcher;

    // Sessions launched and not yet seen in the registry, with when: only these can be at Claude Code's folder trust
    // question. Trust given to an added folder is never recorded, so the records alone would keep a running session
    // that the registry misses (after /clear, say) at that question for good.
    private readonly Dictionary<string, DateTimeOffset> _notYetRegistered = new(StringComparer.OrdinalIgnoreCase);
    private readonly IPlanStore _plans;
    private readonly ITabColourAllocator _colours;
    private readonly PullRequestResolver _pullRequests;
    private readonly GitRemoteReader _remotes = new();
    private CiStatusViewModel? _ciStatus;
    private readonly List<StopStep> _stopQueue = [];
    private readonly IStopRequestStore _stopRequests = new StopRequestStore(StopRequestRoot.Default);
    private readonly IRunLog _runs;

    /// <summary>The questions sessions have open for the owner (1.6), shared with ask_owner and the hook.</summary>
    public Theseus.Core.Questions.OwnerQuestionStore Questions { get; } = new(Theseus.Core.Questions.OwnerQuestionRoot.Default);

    /// <summary>The prompts sessions are stopped at, as the hook recorded them (1.6).</summary>
    public Theseus.Core.Questions.PermissionPromptStore Prompts { get; } = new(Theseus.Core.Questions.PermissionPromptStore.DefaultRoot);
    private readonly HashSet<string> _handoffWriteFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ISessionTodoReader _todos;
    private readonly RunEndings _endings;
    private readonly IWorkflowActivityReader _activity =
        new WorkflowActivityReader(WorkflowRunReader.DefaultDirectory);
    private readonly ClaudeAccountSource _account = new();
    private readonly TranscriptFacts _transcripts = new();
    private AppTheme _theme;
    private readonly NoticeLine _notice = new();
    private bool _isSessionsPanelOpen;
    private bool _isToolbarVisible;
    private bool _isStatusBarVisible;
    private bool _motionAllowed = true;

    /// <summary>The stops already told to exit (<see cref="ExitsTyped"/>).</summary>
    private readonly ExitsTyped _closed = new();
    private DispatcherTimer? _closeRecheck;

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
    /// Workstreams a Start held back because they still wait on an input. A fresh session with
    /// nothing to read only goes looking for a document that does not exist yet.
    /// </summary>
    private IReadOnlyList<Workstream> _heldForInputs = [];

    /// <summary>How the waiting workstreams were asked to start.</summary>
    private StartMode? _heldForInputsMode;

    /// <summary>Whether that mode was the owner's pick on a card's ▾ menu.</summary>
    private bool _heldForInputsChosenOnTheCard;

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
        _viewHost = new ProjectViewHost(board);
        _runs = runs;
        _todos = todos;
        _endings = endings;
        Sessions = sessions;
        _settings = settings;
        _launcher = launcher;
        _plans = plans;
        _colours = colours;
        _pullRequests = new PullRequestResolver(new GitRemoteReader());

        var dispatcher = Dispatcher.CurrentDispatcher;
        _projectReads = new BackgroundProjectReads(
            ProjectCatalog.Read,
            action => dispatcher.BeginInvoke(action),
            new RememberedProjects(Path.Combine(App.DataDirectory, "project-members.json")));
        _projectWatcher = new ProjectFolderWatcher(() => dispatcher.BeginInvoke(() =>
        {
            _projectReads.MarkStale();
            WireProjects();
        }), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1.5));
        _acceptances = new SpecAcceptanceStore(Path.Combine(App.DataDirectory, "spec-acceptances.json"));
        _reported = new ReportedAcceptanceStore(Path.Combine(App.DataDirectory, "reported-acceptances.json"));
        CloseProjectCommand = new RelayCommand(_ => CloseProject(), _ => IsProjectOpen);
        // With no projects folder, New project opens the setup's folder step rather than sitting greyed out (first-run-setup),
        // and goes on to New project once the setup closes with a folder chosen.
        NewProjectCommand = new RelayCommand(_ =>
        {
            if (!FirstRun!.OpenForNewProject() || !string.IsNullOrWhiteSpace(_settings.Read().ProjectsFolder))
            {
                NewProject();
            }
        });
        BringProjectWindowForwardCommand = new RelayCommand(_ => BringProjectWindowForward(),
            _ => IsProjectOpen && _projectWindow.SessionId is not null && Sessions.Find(_projectWindow.SessionId) is not null);
        launcher.ReadableProjects = () => _projectReads.ReadableNames;
        // Asked at each start, from the settings as they are then; the card's own branch is named after its records folder
        // (review of PR 2c, finding 1). A card with a role of its own, or whose row's branch is none, gets none: Theseus
        // wrote it no branch to make (review of 1.1e PR 6, finding 7).
        launcher.BranchRulesFor = workstream =>
            workstream.Role == WorkstreamRole.None
            && !string.Equals(RowBranchOf(workstream), Theseus.Core.Proposals.ProposalRow.NoBranch, StringComparison.OrdinalIgnoreCase)
            && ProjectBranchRules.GrantedPrimary(workstream.Project, _settings.Read().ProjectBranchRules, PrimaryBranchOf) is { } primary
                ? ProjectBranchRules.Text(primary, workstream.Slug ?? workstream.Key,
                    ProjectBranchRules.GrantOf(workstream.Project, _settings.Read().ProjectBranchRules)!.Kind)
                : null;
        launcher.UnattendedFor = workstream => UnattendedProjects.IsOn(workstream.Project, _settings.Read().UnattendedProjects);
        _projectReads.Changed += (_, _) => WireProjectChoices();
        _theme = settings.Read().Theme;
        _isSessionsPanelOpen = !settings.Read().SessionsPanelCollapsed;
        _isToolbarVisible = !settings.Read().ToolbarHidden;
        _isStatusBarVisible = !settings.Read().StatusBarHidden;
        Board.ChainOf = key => Chains?.ChainName(key);
        Board.WaitsOnOwner = key => Chains?.NeedsYou.Any(row => string.Equals(row.Link.From, key, StringComparison.OrdinalIgnoreCase)) == true;
        _transcripts.Changed += (_, _) => ApplyTranscripts();

        Refreshing = new RefreshRun(() =>
            {
                Refresh();
                _ = CiStatus?.PollAsync(refresh: true);
            },
            async () => Updates is { } updates ? await updates.CheckForRefreshAsync() : "Theseus is not checking for new versions.",
            () => Notice,
            text => SetNotice(string.Equals(text, RefreshRun.Started, StringComparison.Ordinal) ? NoticeSeverity.Information : NoticeSeverity, text),
            async () => await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background));
        RefreshCommand = Refreshing.Command;
        FocusCardCommand = new RelayCommand(FocusCard, CanFocusCard);
        OpenJiraCommand = new RelayCommand(OpenJira, parameter => parameter is string);
        OpenPullRequestCommand = new RelayCommand(OpenPullRequest,
            parameter => parameter is PullRequestLink { Url: not null });

        StartCardCommand = new RelayCommand(StartCard, CanStartCard);
        SelectCardCommand = new RelayCommand(ShowCard);
        ShowCardLinksCommand = new RelayCommand(parameter => ShowCard(parameter, DrawerTab.Links));
        ToggleSessionsPanelCommand = new RelayCommand(_ => IsSessionsPanelOpen = !IsSessionsPanelOpen);
        StopCardCommand = new RelayCommand(StopCard, parameter => CardSteps(parameter).Count > 0);

        StartSelectedCommand = new ReasonedCommand(_ => StartSelected(), _ => SelectionReasons.Start(
            Chosen().Count + Board.ChosenProjects.Count,
            ChosenToStart().Count + (_launcher.CanLaunch ? Board.ChosenProjects.Count(project => project.StartPlan is { CanStart: true }) : 0)));
        StopSelectedCommand = new ReasonedCommand(_ => StopSelected(),
            _ => SelectionReasons.Stop(Chosen().Count + Board.ChosenProjects.Count, ChosenSteps().Count));
        SelectAllCommand = new RelayCommand(_ => ChooseAll(true), _ => Board.ShownItems.Any(item => !item.IsSelected));
        ClearSelectionCommand = new RelayCommand(_ =>
        {
            if (!ProjectViewHost.ClearViewSelection(ProjectView))
            {
                ChooseAll(false);
            }
        }, _ => _viewHost.CanClearSelection(ProjectView));
        InvertSelectionCommand = new RelayCommand(_ => Invert(), _ => Board.ShownItems.Any());
        NextMatchCommand = new RelayCommand(_ => StepMatch(forward: true), _ => Board.Search.IsOn && IsBoardShown);
        PreviousMatchCommand = new RelayCommand(_ => StepMatch(forward: false), _ => Board.Search.IsOn && IsBoardShown);
        OpenMatchCommand = new RelayCommand(_ => OpenMatch(), _ => Board.CurrentMatch is not null && IsBoardShown);
        IncludeArchivedCommand = new RelayCommand(_ => Board.IncludeArchived(), _ => Board.Search.ShowsIncludeArchived);
        Board.Search.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BoardSearchViewModel.Filter))
            {
                CarrySearch(ProjectView);
            }
        };
        SelectLiveCommand = new ReasonedCommand(_ => SelectLive(), _ => SelectionReasons.SelectLive(Board.ShownItems.Count(item => item.IsLive)));
        ArchiveSelectedCommand = new ReasonedCommand(_ => ArchiveSelected(),
            _ => SelectionReasons.Archive(Board.ShowArchived, Chosen().Count, Chosen().Count(card => !card.IsLive)));
        RestoreSelectedCommand = new ReasonedCommand(_ => ArchiveSelected(), _ => SelectionReasons.Restore(Board.ShowArchived, Chosen().Count));
        VerifySelectedWithHoldoutsCommand = new RelayCommand(_ =>
            {
                VerifyWithHoldouts(Chosen()[0]);
                SaySkipped();
            },
            _ => Chosen() is [var only] && CanVerify(only));
        MarkSelectedPausedCommand = new ReasonedCommand(_ => MarkSelected(workstream => workstream with { IsPaused = true }, "paused"),
            _ => SelectionReasons.Mark(Chosen().Count, Chosen().Count(card => !card.IsPaused), "paused"));
        MarkSelectedBlockedCommand = new ReasonedCommand(_ => MarkSelected(workstream => workstream with { IsBlocked = true }, "blocked"),
            _ => SelectionReasons.Mark(Chosen().Count, Chosen().Count(card => !card.IsBlocked), "blocked"));
        ClearSelectedMarksCommand = new ReasonedCommand(_ => MarkSelected(FlaggedStart.Cleared, "unmarked"),
            _ => SelectionReasons.ClearMarks(Chosen().Count, Chosen().Count(card => card.IsPaused || card.IsBlocked)));
        FoldAllCommand = new RelayCommand(_ => FoldEvery(), _ => Board.Cards.Any(card => !card.IsCollapsed));
        UnfoldGridCommand = new RelayCommand(_ => Board.UnfoldGrid(),
            _ => Board.Rows.Any(row => row.IsCollapsed) || Board.StageHeaders.Any(header => header.IsCollapsed));
        PropertiesCommand = new RelayCommand(_ =>
        {
            ShowCard(ProjectSelection.OneCard(Board));
            SaySkipped();
        }, _ => ProjectSelection.OneCard(Board) is not null);
        OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(Path.Combine(App.DataDirectory, "logs")));
        OpenDataFolderCommand = new RelayCommand(_ => OpenFolder(App.DataDirectory));

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
        Drawer.EventsOf = _cardEvents.For;
        Drawer.WhoStarts = WhoStartsCard;

        IReadOnlyList<GuardViewModel> guards = [.. Theseus.Core.Guards.GuardCatalog.All.Select(guard => new GuardViewModel(
            new Theseus.Core.Guards.GuardInstallation(guard, Theseus.Core.Guards.GuardInstallation.UserConfigFolder())))];
        Settings = new SettingsViewModel(settings, Refresh)
        {
            Tags = new TagsViewModel(Board.Tags),
            Guards = guards,
            AutoMode = new AutoModeViewModel(settings,
                new Theseus.Core.Guards.AutoModeEnvironment(Theseus.Core.Guards.GuardInstallation.UserConfigFolder())),
            LockTools = new LockOptionsViewModel(settings,
                () => [.. Board.Cards.Where(card => !card.IsArchived).Select(card => (card.Key, card.Title))]),
            FolderTrust = new FolderTrustViewModel(
                new Theseus.Core.Trust.ClaudeFolderTrust(Theseus.Core.Trust.ClaudeFolderTrust.UserConfigPath(),
                    Path.Combine(App.DataDirectory, "folder-trust-undo.json")),
                () => Board.Cards.Where(card => !card.IsArchived).SelectMany(card => SessionFolders(card.Workstream)))
            {
                Said = text => Notice = text,
            },
        };
        FirstRun = new FirstRunViewModel(settings,
            new Theseus.Core.Setup.StopHookRegistration(Theseus.Core.Guards.GuardInstallation.UserConfigFolder(),
                Path.Combine(AppContext.BaseDirectory, "Theseus.Hook.exe")),
            guards,
            () => new Theseus.Core.Setup.ToolsCheck(
                program => Theseus.Core.Setup.ToolVersions.Of(program, settings.Read().ClaudeExecutablePath),
                () => Theseus.Core.Instructions.InstalledPlugins.Read(Theseus.Core.Guards.GuardInstallation.UserConfigFolder())
                    .Any(plugin => plugin.Name == "agentic-factory")).Run(),
            Refresh);
        Settings.OpenProjectHoldouts = OpenProjectHoldouts;
        Settings.HasProjectCard = project => Board.ProjectCards.Any(each => string.Equals(each.Name, project, StringComparison.OrdinalIgnoreCase));
        Drawer.TagsInUse = Board.Tags.InUse;
        Board.Tags.AskNewTag = () => AskText?.Invoke("New tag", "A tag: letters, digits and hyphens, such as security or waiting-on-dpo", string.Empty);
        Board.Tags.Say = message => Notice = message;
        Board.Tags.Changed += () =>
        {
            if (Drawer.Card is { } open)
            {
                Drawer.Reload(open);
            }
        };

        NewWorkstream = new NewWorkstreamViewModel(plans, colours,
            () => [.. Board.Stages],
            () => [.. Board.Areas],
            created =>
            {
                var into = TakeNewIntoProject();
                Refresh();
                Board.Place(created.Key, created.Stage, created.Area);

                if (created.ParentKey is { } parent)
                {
                    Chains?.FollowedOn(parent, created.Key, created.ReportTo);
                }

                Theseus.Core.Proposals.WorkstreamProposal? approved = null;

                if (created.ProposalId is { } proposal)
                {
                    approved = Chains?.Proposals?.Find(proposal);
                    Chains?.Proposals?.Approved(proposal, created.Key, created.BySession);
                }

                if (Board.Card(created.Key) is not { } card)
                {
                    Warn("The workstream was created but is not on the board yet.");

                    return;
                }

                var joined = approved is null ? null : JoinProposersProject(card, approved);

                if (into is not null && approved is null)
                {
                    AddToProject(card, into);
                }

                if (approved is not null && created.BySession)
                {
                    // Made by its own session, not by the owner, so nothing is told: the session has the answer already.
                    var by = Board.Card(approved.RequesterKey)?.Title ?? approved.RequesterKey;
                    var what = approved.Warnings.Count == 0
                        ? $"Created by {by}"
                        : $"Created by {by}; warnings: {string.Join(" ", approved.Warnings)}";
                    _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, card.Key, what));
                    Theseus.Core.Diagnostics.TheseusLog.Information("{Card}: {What}", card.Key, what);
                    joined = joined is null ? $"{by} created {card.Title}." : $"{by} created {card.Title}. {joined}";
                }
                else if (approved is not null)
                {
                    Tell(Board.Card(approved.RequesterKey), OwnerEvents.Approved(approved.Request.Title, card.Title, card.Key));
                }

                if (created.StartNow)
                {
                    StartMany([card.Workstream], explain: !created.BySession);

                    if (joined is not null)
                    {
                        Extend(string.IsNullOrWhiteSpace(Notice) ? joined : $"{Notice} {joined}");
                    }

                    return;
                }

                Notice = joined ?? $"Created {card.Title} in {card.Column}.";
            },
            () => Chains?.TakenKeys ?? [],
            () => _settings.Read().BriefFolderName);

        NewWorkstream.PropertyChanged += OnNewWorkstreamChanged;
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

        StartWithoutInputsCommand = new RelayCommand(_ =>
        {
            var held = _heldForInputs;
            var mode = _heldForInputsMode;
            var chosenOnTheCard = _heldForInputsChosenOnTheCard;

            _heldForInputs = [];
            InputsChanged();
            StartMany(held, evenIfWaiting: true, startMode: mode, chosenOnTheCard: chosenOnTheCard, trigger: "StartWithoutInputs");
        });

        SkipWaitingCommand = new RelayCommand(_ =>
        {
            var count = _heldForInputs.Count;

            _heldForInputs = [];
            InputsChanged();
            Notice = count == 1 ? "Left the waiting card unstarted." : $"Left {count} waiting cards unstarted.";
        });

        CollapseInactiveCommand = new ReasonedCommand(_ => Collapse(true),
            _ => SelectionReasons.FoldInactive(Board.Cards.Count(card => !card.IsLive && !card.IsCollapsed)
                + Board.ProjectCards.Count(project => Board.FoldsWhenInactive(project) && !project.IsCollapsed)));
        ExpandAllCommand = new ReasonedCommand(_ => Collapse(false), _ => SelectionReasons.UnfoldAll(Board.Cards.Count(card => card.IsCollapsed)
            + Board.ProjectCards.Count(project => project.IsCollapsed)));

        SelectProjectTabCommand = new RelayCommand(number => OpenTab(ProjectTabKeys.Numbered(number, TabNames().Count)),
            number => ProjectTabKeys.Numbered(number, TabNames().Count) is not null);
        NextProjectTabCommand = new RelayCommand(_ => OpenTab(ProjectTabKeys.Step(OpenTabIndex(), TabNames().Count, forward: true)),
            _ => ProjectTabKeys.Step(OpenTabIndex(), TabNames().Count, forward: true) is not null);
        PreviousProjectTabCommand = new RelayCommand(_ => OpenTab(ProjectTabKeys.Step(OpenTabIndex(), TabNames().Count, forward: false)),
            _ => ProjectTabKeys.Step(OpenTabIndex(), TabNames().Count, forward: false) is not null);
        MakeLanesEvenCommand = new ReasonedCommand(_ => MakeLanesEven(),
            _ => Board.StageHeaders.Any(header => Math.Abs(header.Weight - 1) > 0.001) || Board.Rows.Any(row => !double.IsNaN(row.Height))
                ? null
                : "The lanes are already even.");

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

    /// <summary>What the sessions and the machine are using, for the cards and the status bar.</summary>
    public ResourcesViewModel? Resources { get; set; }

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

    /// <summary>Re-reads everything on disk, and checks for a new version; disabled while it runs.</summary>
    public RelayCommand RefreshCommand { get; }

    /// <summary>Refresh as it runs: busy while the owner waits on it, for the busy cursor (board-small-changes item 1).</summary>
    public RefreshRun Refreshing { get; }

    /// <summary>Brings a card's session to the front.</summary>
    public RelayCommand FocusCardCommand { get; }

    /// <summary>Opens a Jira issue in the browser.</summary>
    public RelayCommand OpenJiraCommand { get; }

    /// <summary>Opens a pull request in the browser.</summary>
    public RelayCommand OpenPullRequestCommand { get; }

    /// <summary>Starts one workstream.</summary>
    public RelayCommand StartCardCommand { get; }

    /// <summary>Starts the ticked cards that can be started.</summary>
    public ReasonedCommand StartSelectedCommand { get; }

    /// <summary>Ticks every card shown: the board's, or the open project view's.</summary>
    public RelayCommand SelectAllCommand { get; }

    /// <summary>Unticks every card.</summary>
    public RelayCommand ClearSelectionCommand { get; }

    /// <summary>Enter or F3 in the search: selects the next match (card-search-and-tags item 6).</summary>
    public RelayCommand NextMatchCommand { get; }

    /// <summary>Shift+F3: selects the match before (item 6).</summary>
    public RelayCommand PreviousMatchCommand { get; }

    /// <summary>Ctrl+Enter: opens the match the keys moved to, its drawer or its project view (item 6).</summary>
    public RelayCommand OpenMatchCommand { get; }

    /// <summary>Include archived, offered when only an archived card matches (item 5).</summary>
    public RelayCommand IncludeArchivedCommand { get; }

    /// <summary>A match the keys moved to, for the window to scroll into view (item 6).</summary>
    public event Action<IBoardCard>? MatchShown;

    private void StepMatch(bool forward)
    {
        if (Board.StepMatch(forward) is { } item)
        {
            MatchShown?.Invoke(item);
        }
    }

    private void OpenMatch()
    {
        switch (Board.CurrentMatch)
        {
            case WorkstreamCardViewModel card:
                ShowCard(card);
                break;
            case ProjectCardViewModel project:
                project.OpenCommand?.Execute(null);
                break;
        }
    }

    /// <summary>
    /// Carries the board's search into a project view (item 7): the boxes <see cref="SearchCarry"/> matches are highlighted
    /// the way a followed objective's are. Null or no search clears it.
    /// </summary>
    private void CarrySearch(ProjectViewModel? view)
    {
        if (view is null)
        {
            return;
        }

        var filter = Board.Search.Filter;

        if (!filter.IsOn)
        {
            view.ShowSearch(null);

            return;
        }

        view.ShowSearch(node => SearchCarry.Matches(node, key => Board.Card(key) is { } card ? Board.IsMatch(card) : null, filter),
            key => Board.Card(key) is { } card && Board.IsMatch(card));
    }

    /// <summary>Ticks what is not ticked and unticks what is.</summary>
    public RelayCommand InvertSelectionCommand { get; }

    /// <summary>Folds every card with no live session down to its title.</summary>
    public ReasonedCommand CollapseInactiveCommand { get; }

    /// <summary>Opens every card again.</summary>
    public ReasonedCommand ExpandAllCommand { get; }

    /// <summary>Ctrl+1 to Ctrl+9: opens the project tab of that number, Ctrl+9 the last (W27).</summary>
    public RelayCommand SelectProjectTabCommand { get; }

    /// <summary>
    /// View › Make lanes even: every column back to an even share and every row fitting its cards, the keyboard's way to
    /// what dragging the lane edges does (B17, G64).
    /// </summary>
    public ReasonedCommand MakeLanesEvenCommand { get; }

    private void MakeLanesEven()
    {
        foreach (BoardRowViewModel row in Board.Rows)
        {
            Board.ResetRowHeight(row);
        }

        Board.ResetColumnWidths();
    }

    /// <summary>Ctrl+Tab: opens the next project tab, while a project is open.</summary>
    public RelayCommand NextProjectTabCommand { get; }

    /// <summary>Ctrl+Shift+Tab: opens the previous project tab, while a project is open.</summary>
    public RelayCommand PreviousProjectTabCommand { get; }

    // The tabs' order: the readable projects, as ProjectTabViewModel.For lists them.
    private List<string> TabNames() => [.. _projectReads.Current.Where(reading => reading.IsReadable).Select(reading => reading.Name)];

    private int? OpenTabIndex()
    {
        if (!IsProjectOpen)
        {
            return null;
        }

        int index = TabNames().FindIndex(name => string.Equals(name, _openProjectName, StringComparison.OrdinalIgnoreCase));

        return index < 0 ? null : index;
    }

    private void OpenTab(int? index)
    {
        List<string> names = TabNames();

        if (index is { } at && at < names.Count && at != OpenTabIndex())
        {
            OpenProject(names[at]);
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

    /// <summary>How many cards and project cards are ticked, so the buttons can say what they would act on.</summary>
    public string SelectionSummary => Board.ShownItems.Count(item => item.IsSelected) is var count && count > 0
        ? $"{count} selected"
        : "none selected";

    /// <summary>Properties' tooltip: what it opens, or why not for a project.</summary>
    public string PropertiesToolTip => ProjectSelection.ToolTip(Board, "The selected card's properties");

    /// <summary>Independent check's tooltip, on the Workstream menu: what it does, or why not for a project.</summary>
    public string VerifySelectedToolTip => Chosen() is [var only] && WhyCannotVerify(only) is { } why ? why : ProjectSelection.ToolTip(Board,
        "Create a verifier card for the one ticked card: it runs the holdout scenarios its spec names, and reports only a verdict back. Needs one card ticked and the holdout scenarios folder set in Tools > Options; not offered for a verifier card or an archived one");

    /// <summary>
    /// Why Independent check… on the Workstream menu cannot run for the one ticked card, or null: the item's dimmer second
    /// line under its fixed label (Nigel, 2026-11-23; W30).
    /// </summary>
    public string? VerifySelectedOffReason => Chosen() is [WorkstreamCardViewModel only] ? WhyCannotVerify(only) : null;

    /// <summary>Ticks every card with a session running, so the end of the day is Select live, then Stop.</summary>
    public ReasonedCommand SelectLiveCommand { get; }

    /// <summary>Archives the ticked cards that have no session running; not while the archive is showing (W27, B6).</summary>
    public ReasonedCommand ArchiveSelectedCommand { get; }

    /// <summary>Restores the ticked cards, while the archive is showing: Archive's fixed pair (W30).</summary>
    public ReasonedCommand RestoreSelectedCommand { get; }

    /// <summary>Opens the properties of the one selected card: Alt+Enter, as everywhere in Windows.</summary>
    public RelayCommand PropertiesCommand { get; }

    /// <summary>
    /// Shows a card that has just been clicked in the properties panel, if that panel is open, so it
    /// follows the selection the way a reading pane does.
    /// </summary>
    public void Clicked(WorkstreamCardViewModel card)
    {
        if (Drawer.IsOpen && Chosen() is { Count: 1 } chosen)
        {
            ShowCard(chosen[0]);
        }
    }

    /// <summary>Marks the ticked cards paused.</summary>
    public ReasonedCommand MarkSelectedPausedCommand { get; }

    /// <summary>Marks the ticked cards blocked.</summary>
    public ReasonedCommand MarkSelectedBlockedCommand { get; }

    /// <summary>Takes the paused and blocked marks off the ticked cards.</summary>
    public ReasonedCommand ClearSelectedMarksCommand { get; }

    /// <summary>Folds every card down to its title, live or not.</summary>
    public RelayCommand FoldAllCommand { get; }

    /// <summary>Opens every folded row and stage.</summary>
    public RelayCommand UnfoldGridCommand { get; }

    /// <summary>Opens the folder Theseus writes its logs to.</summary>
    public RelayCommand OpenLogsFolderCommand { get; }

    /// <summary>Opens the folder Theseus keeps its board, settings and runs in.</summary>
    public RelayCommand OpenDataFolderCommand { get; }

    /// <summary>
    /// Asks whether workstreams marked paused or blocked should start: true to clear the marks and
    /// start them, false to leave them and start the rest, null to start nothing. Unset, the marked
    /// ones are left alone.
    /// </summary>
    public Func<string, bool, bool?>? ConfirmFlaggedStart { get; set; }

    /// <summary>
    /// Asks before one card of a running project is stopped or started by hand (1.1e AC 10): the question and the action
    /// button's label; true when the action was chosen. Unset, nothing is stopped or started.
    /// </summary>
    public Func<string, string, bool>? ConfirmProjectCard { get; set; }

    /// <summary>Shows a project's properties; true when OK was pressed (1.1d AC 40). Unset, nothing changes.</summary>
    public Func<ProjectPropertiesViewModel, bool>? AskProjectProperties { get; set; }

    /// <summary>Says in the status bar why a drag could not be dropped (1.1d AC 42).</summary>
    public void DropRefused(string reason) => Warn(reason);

    /// <summary>
    /// What a project's session does to its descendants, through the Theseus tools (1.1e AC 14 to 16), on the board's own
    /// start and stop. Called on the UI thread.
    /// </summary>
    public IWorkstreamControl WorkstreamControl => _workstreamControl ??= new BoardWorkstreamControl(Board,
        StartForSession,
        StopForSession,
        HandInForSession,
        _cardEvents,
        text => Extend(string.IsNullOrWhiteSpace(Notice) ? text : $"{Notice} {text}"),
        () => DateTimeOffset.UtcNow,
        Refresh)
    {
        IsProjectClosed = IsProjectClosed,
    };

    /// <summary>
    /// A session's start of a descendant (review of PR 3, findings 5 and 6): it resumes the conversation, falling back to
    /// fresh only when there is none, and counts only once a run is recorded; otherwise it says why, from what the start
    /// said, so the session does not try again blindly.
    /// </summary>
    private string? StartForSession(IReadOnlyList<Workstream> workstreams, string trigger)
    {
        var key = workstreams[0].Key;
        int Runs() => _runs.Read().Count(run => string.Equals(run.WorkstreamKey, key, StringComparison.OrdinalIgnoreCase));
        var before = Runs();
        var said = Notice;
        NoticeSeverity saidSeverity = NoticeSeverity;

        Notice = string.Empty;
        // A session's start answers the session; a dialog here would hold every MCP call behind the owner (review, 2026-11-25).
        StartMany(workstreams, startMode: StartMode.Resume, explain: false, trigger: trigger);

        if (Runs() > before)
        {
            return null;
        }

        var why = string.IsNullOrWhiteSpace(Notice) ? "Theseus could not start it just now" : Notice;
        if (string.IsNullOrWhiteSpace(Notice))
        {
            SetNotice(saidSeverity, said);
        }

        return $"{workstreams[0].Title} did not start: {why}";
    }

    /// <summary>
    /// A session's stop of a descendant: the stop request, which its session reads when its
    /// turn ends; an idle session, which takes no turn of its own, is woken through its console to take it at once. It
    /// never cuts a turn short, never touches the owner's own Stop all, and never hands the owner text to paste; without
    /// the stop hook it says the user must stop it.
    /// </summary>
    private string? StopForSession(WorkstreamCardViewModel card)
    {
        if (!HookInstallation.IsInstalled(SettingsFile()))
        {
            return $"Theseus's stop hook is not installed, so {card.Title} cannot be asked to stop from here; the user must stop it.";
        }

        if (StepsFor([card]).FirstOrDefault() is not { } step)
        {
            return $"{card.Title} has no session to stop.";
        }

        // The close step first: it retires a stop handed to an earlier run, which would otherwise hold back this one, and
        // closes a session that already has its stop and has answered it.
        CloseFinishedSessions();

        if (StillHandingOver(step))
        {
            return null;
        }

        if (_stopRequests.Waiting(step.SessionId) is null)
        {
            Ask(step);
        }

        if (step.Lifecycle == SessionLifecycle.Idle)
        {
            Wake(step, StopRouting.NudgeMarker);
        }

        return null;
    }

    /// <summary>
    /// Starts and stops whole projects with the board's own Start and Stop (1.1e AC 8, 9). What a project's Start skipped
    /// is added to what the start itself said, or said alone (review of PR 2, finding 7).
    /// </summary>
    private ProjectRunner Runner => _runner ??= new ProjectRunner(Board.Card, _settings,
        workstreams =>
        {
            var before = Notice;
            StartMany(workstreams, trigger: "StartProject");
            _projectStartSaid = !string.Equals(before, Notice, StringComparison.Ordinal);
        },
        cards => BeginStop(StepsFor(cards)),
        text =>
        {
            Extend(_projectStartSaid && !string.IsNullOrWhiteSpace(Notice) ? $"{Notice} {text}" : text);
            _projectStartSaid = false;
        },
        Refresh,
        project =>
        {
            if (Readable(project.Name) is { File: { } file, Folder: { } home })
            {
                StartLead(project.Name, file.Title, home);
            }
            else
            {
                Warn($"{project.Title} does not read, so its lead card cannot be made.");
            }
        });

    /// <summary>A card's and a selection's Start and Stop, with the questions and marks of a project that owns what runs (1.1e AC 10 to 12).</summary>
    private CardRunCommands RunCommands => _runCommands ??= new CardRunCommands(_settings, () => Board.ProjectCards, Runner,
        (question, verb) => ConfirmProjectCard?.Invoke(question, verb) == true,
        (title, label, initial) => AskText?.Invoke(title, label, initial),
        (workstreams, trigger) => StartMany(workstreams, trigger: trigger),
        cards => BeginStop(StepsFor(cards)));

    /// <summary>Start selected (1.1e AC 10, 11).</summary>
    private void StartSelected()
    {
        var startable = ChosenToStart();

        RunCommands.StartSelected([.. startable.Select(card => Board.Card(card.Key)).OfType<WorkstreamCardViewModel>()], startable,
            Board.ChosenProjects);
    }

    /// <summary>Stop selected (1.1e AC 8, 10, 11).</summary>
    private void StopSelected() => RunCommands.StopSelected(Chosen(), Board.ChosenProjects);

    /// <summary>
    /// Resume or Start fresh on a card's menu, for it or the selection it is in: asked about first when a card is in a
    /// running project, as ▶ is (review of PR 2, finding 3).
    /// </summary>
    private void StartChosen(WorkstreamCardViewModel card, StartMode mode, string trigger)
    {
        var targets = Targets(card).Where(CanStartCard).ToList();

        if (RunCommands.Allowed(targets, "Start"))
        {
            StartMany([.. targets.Select(target => target.Workstream)], startMode: mode, chosenOnTheCard: true, trigger: trigger);
        }
    }

    /// <summary>A card's own ■ (1.1e AC 10).</summary>
    private void StopCard(object? parameter)
    {
        if (parameter is WorkstreamCardViewModel card && RunCommands.StopCard(card))
        {
            BeginStop(StepsFor([card]));
        }
    }

    /// <summary>
    /// The primary branch a card may work from under its project's branch rules, or null (1.1e AC 12b): only while the
    /// owner's grant is in the settings and the project file, as read now, names a primary branch.
    /// </summary>
    private string? PrimaryBranchOf(string project) => Readable(project)?.File?.PrimaryBranch;

    /// <summary>
    /// When a card's project was granted branch rules, while they apply, or null; from the grants read once per refresh
    /// (review of PR 2c, finding 7).
    /// </summary>
    private DateTimeOffset? BranchRulesSince(Workstream workstream) =>
        ProjectBranchRules.GrantedPrimary(workstream.Project, _branchGrants, PrimaryBranchOf) is not null
            ? ProjectBranchRules.GrantOf(workstream.Project, _branchGrants)!.Since
            : null;

    /// <summary>A project's title from its file, for "resumes with &lt;project&gt;" (review of PR 2, finding 10).</summary>
    public string ProjectTitle(string name) =>
        _projectReads.Current.FirstOrDefault(reading => string.Equals(reading.Name, name, StringComparison.OrdinalIgnoreCase))?.File?.Title
        ?? name;

    private string ProjectsSkipped() => ProjectSelection.Skipped(Board);

    /// <summary>Adds to the status bar that a command for one workstream passed over the selected projects.</summary>
    private void SaySkipped()
    {
        if (ProjectsSkipped() is { Length: > 0 } skipped)
        {
            Extend(string.IsNullOrWhiteSpace(Notice) ? skipped.Trim() : $"{Notice}{skipped}");
        }
    }

    /// <summary>
    /// Project properties…: the project's title, colour, tags, lead, branch rules and holdout folder (1.1d AC 40;
    /// options-made-clear item 1), opened at the tab named, or the first.
    /// </summary>
    private void ShowProjectProperties(ProjectCardViewModel card, string? openAt = null)
    {
        var chosenHoldouts = _settings.Read().ProjectHoldoutFolders;
        var chosenHoldout = chosenHoldouts.FirstOrDefault(pair => string.Equals(pair.Key, card.Name, StringComparison.OrdinalIgnoreCase)).Value;
        var others = Board.ProjectCards
            .Where(other => !string.Equals(other.Name, card.Name, StringComparison.OrdinalIgnoreCase))
            .GroupBy(other => other.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().ColourHex, StringComparer.CurrentCultureIgnoreCase);
        var primary = PrimaryBranchOf(card.Name);
        var grant = ProjectBranchRules.GrantOf(card.Name, _settings.Read().ProjectBranchRules);
        var granted = grant is not null;
        var file = Readable(card.Name)?.File;
        var lead = file is null ? null : ProjectLead.KeyOf(file, _settings.Read().ProjectLeads);
        var tags = new TagEditorViewModel(() => [.. Board.Tags.InUse().Select(use => use.Tag)], () => { });
        tags.Load(card.Tags);
        var properties = new ProjectPropertiesViewModel(card.Name, card.Title, card.ColourHex, card.ProjectFile, others)
        {
            TagEditor = tags,
            Leads = file is null
                ? []
                : [.. file.Workstreams.Select(row => row.Workstream).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(key => new KeyValuePair<string, string>(key, $"{Board.Card(key)?.Title ?? key} ({key})"))],
            Lead = lead,
            Problem = card.Problem,
            PrimaryBranch = primary,
            GrantsBranchRules = granted,
            GrantsItemBranches = grant?.Kind == BranchGrantKind.ItemBranches,
            GrantedPrimary = grant?.Primary,
            IsUnattended = UnattendedProjects.IsOn(card.Name, _settings.Read().UnattendedProjects),
            RunsHeadless = _settings.Read().ProjectRunModes.TryGetValue(card.Name, out var projectMode) && projectMode == RunMode.Headless,
            ResumesIdleCards = _settings.Read().Duties.QueueIsOn(card.Name),
            OpenAt = openAt,
            Holdout = _holdouts.LocationOfProject(card.Name),
            HoldoutByConvention = HoldoutLocation.Find(card.Name, null, HoldoutConventionOf(card.Name)),
            ChosenHoldout = chosenHoldout,
            // The title is written at OK, and a refusal is shown in the dialog (1.1e AC 12a).
            WriteTitle = title => EditProject(card.Name, text => ProjectFileEditor.SetTitle(text, card.Title, title),
                $"{card.Title} is now called {title}.")
                ? null
                : Notice,
        };

        if (AskProjectProperties?.Invoke(properties) != true)
        {
            return;
        }

        if (properties.HoldoutChanged)
        {
            var current = _settings.Read();
            var holdouts = new Dictionary<string, string>(current.ProjectHoldoutFolders, StringComparer.OrdinalIgnoreCase);
            holdouts.Remove(card.Name);

            if (properties.HoldoutChoice is { } folder)
            {
                holdouts[card.Name] = folder;
            }

            _settings.Write(current with { ProjectHoldoutFolders = holdouts });
            RefreshHoldouts([.. Board.Cards.Select(each => each.Workstream)]);
            Notice = properties.HoldoutChoice is { } chosenFolder
                ? $"{card.Title}'s holdout folder is now {chosenFolder}."
                : _holdouts.LocationOfProject(card.Name) is { Folder.Length: > 0 } found
                    ? $"{card.Title}'s holdout folder is found by convention again: {found.Folder}."
                    : $"{card.Title}'s holdout folder is found by convention again, but its recorded repository is not found.";
        }

        if (properties.ResumesIdleCards != _settings.Read().Duties.QueueIsOn(card.Name))
        {
            var current = _settings.Read();
            IReadOnlyList<string> queued = properties.ResumesIdleCards
                ? [.. current.Duties.QueueProjects, card.Name]
                : [.. current.Duties.QueueProjects.Where(name => !string.Equals(name, card.Name, StringComparison.OrdinalIgnoreCase))];
            _settings.Write(current with { Duties = current.Duties with { QueueProjects = queued } });
            TheseusLog.Information("Overnight queue switched {State} for {Project} by the owner", properties.ResumesIdleCards ? "on" : "off", card.Name);
            Notice = $"The overnight queue is {(properties.ResumesIdleCards ? "on" : "off")} for {card.Title}.";
        }

        tags.AddPending();
        var merged = tags.MergeInto(card.Tags);

        if (!merged.SequenceEqual(card.Tags))
        {
            Board.Tags.SetProject(card.Name, merged);
        }

        if (properties.Lead is { } chosen && !string.Equals(chosen, lead, StringComparison.OrdinalIgnoreCase))
        {
            var current = _settings.Read();
            _settings.Write(current with
            {
                ProjectLeads = new Dictionary<string, string>(current.ProjectLeads, StringComparer.OrdinalIgnoreCase) { [card.Name] = chosen },
            });
            Notice = $"{card.Title}'s lead is now {Board.Card(chosen)?.Title ?? chosen}.";
            WireProjectChoices();
        }

        var modes = _settings.Read().ProjectRunModes;

        if (properties.RunsHeadless != (modes.TryGetValue(card.Name, out var before) && before == RunMode.Headless))
        {
            var changed = modes.Where(pair => !string.Equals(pair.Key, card.Name, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

            if (properties.RunsHeadless)
            {
                changed[card.Name] = RunMode.Headless;
            }

            _settings.Write(_settings.Read() with { ProjectRunModes = changed });
            Notice = properties.RunsHeadless
                ? $"{card.Title}'s cards run headless from their next start, unless a card says otherwise."
                : $"{card.Title}'s cards run as their roles say from their next start: verifiers headless, the rest in a tab.";
        }

        // OK with the box ticked over a grant for another primary branch grants it for the one the file names now.
        var kind = properties.GrantsItemBranches ? BranchGrantKind.ItemBranches : BranchGrantKind.MergeIntoPrimary;

        if (properties.GrantsBranchRules != granted
            || (granted && properties.GrantsBranchRules && !string.Equals(grant!.Primary, primary?.Trim(), StringComparison.Ordinal))
            || (granted && properties.GrantsBranchRules && grant!.Kind != kind))
        {
            GrantBranchRules(card, properties.GrantsBranchRules ? primary : null, kind);
        }

        if (properties.IsUnattended != UnattendedProjects.IsOn(card.Name, _settings.Read().UnattendedProjects))
        {
            var current = _settings.Read();
            _settings.Write(current with { UnattendedProjects = UnattendedProjects.Set(current.UnattendedProjects, card.Name, properties.IsUnattended) });
            TheseusLog.Information("{Project} switched {State} by the owner", card.Name, properties.IsUnattended ? "to unattended" : "back to attended");
            Notice = properties.IsUnattended
                ? $"{card.Title} runs unattended from each card's next start: questions are parked, finished cards end, and network errors are retried."
                : $"{card.Title}'s cards may ask you again from their next start.";
        }

        if (string.Equals(properties.ColourHex, card.ColourHex, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var settings = _settings.Read();
        var colours = new Dictionary<string, string>(settings.ProjectColours, StringComparer.OrdinalIgnoreCase)
        {
            [card.Name] = properties.ColourHex,
        };

        _settings.Write(settings with { ProjectColours = colours });
        WireProjectChoices();
        Extend(string.IsNullOrWhiteSpace(Notice) || !properties.TitleChanged
            ? $"{properties.Title.Trim()} has a new colour."
            : $"{Notice} It has a new colour.");
    }

    private static string ProblemOf(ProjectFileReading reading) => ProjectUpdate.ProblemOf(reading);

    /// <summary>
    /// The updater card of a project, if one has been made and is not archived: in its folder, with its role (review of PR
    /// 4, finding 3).
    /// </summary>
    private WorkstreamCardViewModel? UpdaterOf(ProjectFileReading reading) =>
        Board.Cards.FirstOrDefault(card => card.Workstream is { Role: ProjectUpdaterCard.Role, IsArchived: false }
                                           && reading.Folder is { } folder
                                           && string.Equals(Path.TrimEndingDirectorySeparator(card.Workstream.WorkingDirectory),
                                               Path.TrimEndingDirectorySeparator(folder), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Update on a project card whose file does not read (1.1e AC 19): starts its updater card, made on first use, unless
    /// one of the project's own cards is running or the updater already is.
    /// </summary>
    private void UpdateProjectFile(string name)
    {
        if (_projectReads.Current.FirstOrDefault(reading => string.Equals(reading.Name, name, StringComparison.OrdinalIgnoreCase)) is not
            { IsReadable: false, Folder: { } folder } reading)
        {
            return;
        }

        // A card is the project's when its file has ever named it, which the reads remember after the file stops reading,
        // or when it works in the project's folder (review of PR 4, finding 1).
        var known = _projectReads.History.CardsOf(name);
        var cards = Board.Cards
            .Where(card => card.Workstream.Role != ProjectUpdaterCard.Role)
            .Select(card => (card.Title, card.IsLive,
                Member: ProjectUpdate.IsMember(card.Key, card.Workstream.WorkingDirectory, known, folder) ? name : null));

        if (ProjectUpdate.Refusal(name, cards, UpdaterOf(reading) is { IsLive: true }) is { } refusal)
        {
            Warn(refusal);

            return;
        }

        var problem = ProblemOf(reading);
        var updater = ProjectUpdaterCard.For(name, folder, problem, ProjectStarter.ShapeText());
        var earlier = UpdaterOf(reading);
        var key = earlier?.Key ?? WorkstreamKey.Unique(updater.Title, [.. _plans.Keys(), .. Chains?.TakenKeys ?? []]);

        // Its prompt is written afresh each time, with the problem and shape of now (review of PR 4, finding 4).
        _plans.Write(key, _plans.TryRead(key) is { } plan
            ? plan with { Prompt = updater.Prompt }
            : new SessionPlan(_plans.Keys().Count + 1, updater.Title, ProjectCardViewModel.NoColour, updater.WorkingDirectory, [],
                null, DateTimeOffset.Now.ToString("s"), updater.Prompt));
        Refresh();

        if (Board.Card(key) is not { } card)
        {
            return;
        }

        if (card.Workstream.Role != ProjectUpdaterCard.Role)
        {
            card.Workstream = card.Workstream with { Role = ProjectUpdaterCard.Role };
            Board.Persist(card);
            TheseusLog.Information("Created {Updater} to update the project file of {Project}", key, name);
        }

        // A start again resumes its conversation, which never sees the rewritten prompt, so it is told the problem of now.
        var start = card.Workstream with
        {
            Prompt = updater.Prompt,
            StartInstruction = earlier is null ? null : $"{updater.Prompt.Split(Environment.NewLine)[0]} Bring it into the shape your opening prompt gave, as before.",
        };

        if (StartForSession([start], "UpdateProjectFile") is { } notStarted)
        {
            Warn(notStarted);

            return;
        }

        // That it ran, so the card says Still needs updating until the file reads, and the file it started on, so it is asked
        // to stop once the file has changed and its turn has ended; kept in settings, across a restart (review of PR 4).
        var settings = _settings.Read();
        _settings.Write(settings with
        {
            ProjectUpdaterRuns = new Dictionary<string, string>(settings.ProjectUpdaterRuns, StringComparer.OrdinalIgnoreCase)
            {
                [name] = ProjectUpdate.Fingerprint(reading.Text),
            },
        });
        WireProjectChoices();
    }

    /// <summary>
    /// Asks a running updater to write its handover and exit once its project's file has changed since it started and its
    /// turn has ended (review of PR 4, finding 2 and its re-check): a session cannot leave by itself. It uses the stop
    /// request alone, as a session's stop does, so the owner's Stop all queue is untouched. A project that reads, with no
    /// updater running, leaves the list, so a later break starts from Needs updating. Returns whether anything changed.
    /// </summary>
    private bool ReviewUpdaters(IReadOnlyList<ProjectFileReading> readings)
    {
        var runs = new Dictionary<string, string>(_settings.Read().ProjectUpdaterRuns, StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (name, startedOn) in runs.ToList())
        {
            var reading = readings.FirstOrDefault(each => string.Equals(each.Name, name, StringComparison.OrdinalIgnoreCase));
            var updater = reading is null ? null : UpdaterOf(reading);

            if (reading is not null && updater is { IsLive: true } && Sessions.Find(updater.Workstream.LastSessionId) is not null
                && ProjectUpdate.IsDone(startedOn, reading, updater.Lifecycle is SessionLifecycle.Idle or SessionLifecycle.NeedsAttention))
            {
                TheseusLog.Information("Asking {Updater} to stop: the project file of {Project} has changed and its turn has ended", updater.Key, name);

                if (StopForSession(updater) is { } cannot)
                {
                    Warn(cannot);
                }

                runs[name] = string.Empty;
                changed = true;
            }

            if (reading is null || (reading.IsReadable && updater is not { IsLive: true }))
            {
                runs.Remove(name);
                changed = true;
            }
        }

        if (changed)
        {
            _settings.Write(_settings.Read() with { ProjectUpdaterRuns = runs });
        }

        return changed;
    }

    /// <summary>Not a project, on a project card whose file does not read (1.1e AC 20): its folder's card is hidden.</summary>
    private void HideProjectFolder(string name)
    {
        var settings = _settings.Read();
        _settings.Write(settings with { HiddenProjectFolders = [.. settings.HiddenProjectFolders.Append(name).Distinct(StringComparer.OrdinalIgnoreCase)] });
        Notice = $"{name} is no longer shown as a project. Tools > Options lists it, to show it again.";
        if (!Settings.HiddenProjectFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            Settings.HiddenProjectFolders.Add(name);
        }
        WireProjectChoices();
    }

    /// <summary>
    /// Grants or withdraws a project's branch rules, the owner's act in Project properties (1.1e AC 12b), kept in the
    /// settings with the date; its cards are told from their next start on.
    /// </summary>
    /// <param name="card">The project's card.</param>
    /// <param name="primary">The primary branch to grant, as the file names it now, or null to withdraw.</param>
    /// <param name="kind">Which rules are granted.</param>
    private void GrantBranchRules(ProjectCardViewModel card, string? primary, BranchGrantKind kind = BranchGrantKind.MergeIntoPrimary)
    {
        var settings = _settings.Read();
        var rules = new Dictionary<string, ProjectBranchGrant>(settings.ProjectBranchRules, StringComparer.OrdinalIgnoreCase);
        var grant = primary is not null && ProjectBranchRules.HasPrimary(primary);

        if (grant)
        {
            rules[card.Name] = new ProjectBranchGrant(DateTimeOffset.Now, primary!.Trim()) { Kind = kind };
        }
        else
        {
            rules.Remove(card.Name);
        }

        _settings.Write(settings with { ProjectBranchRules = rules });
        TheseusLog.Information("Branch rules for {Project} {Change} by the owner", card.Name, grant ? "granted" : "withdrawn");
        Notice = grant
            ? kind == BranchGrantKind.ItemBranches
                ? $"{card.Title}'s cards may now branch each item off its repository's trunk, push and raise a pull request without asking, from their next start."
                : $"{card.Title}'s cards may now branch, push and merge into {primary!.Trim()} without asking, from their next start."
            : $"{card.Title}'s cards ask again before any branch or push, from their next start.";
        Refresh();
    }

    /// <summary>
    /// Every project card's colour from the project palette, given to any project that has none yet, and saved (1.1d AC
    /// 40; decision 7).
    /// </summary>
    private IReadOnlyDictionary<string, string> ProjectColoursFor(IReadOnlyList<string> names)
    {
        var saved = _settings.Read().ProjectColours;
        var assigned = ProjectColours.Assign(saved, names);

        if (!ReferenceEquals(assigned, saved))
        {
            _settings.Write(_settings.Read() with { ProjectColours = assigned });
        }

        return assigned;
    }

    /// <summary>The Jira issues and pull requests sessions recorded against their cards, with titles.</summary>
    public WorkItemBook? WorkItems { get; set; }

    /// <summary>The board's recent notices, for the status bar's list.</summary>
    public NoticeLog Notices { get; } = new();

    /// <summary>Whether the toolbar is showing. Remembered between runs.</summary>
    public bool IsToolbarVisible
    {
        get => _isToolbarVisible;
        set
        {
            if (Set(ref _isToolbarVisible, value))
            {
                _settings.Write(_settings.Read() with { ToolbarHidden = !value });
            }
        }
    }

    /// <summary>Whether the status bar is showing. Remembered between runs.</summary>
    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set
        {
            if (Set(ref _isStatusBarVisible, value))
            {
                _settings.Write(_settings.Read() with { StatusBarHidden = !value });
            }
        }
    }

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

    /// <summary>Where this copy of Theseus is installed, for About.</summary>
    public string InstallFolder => AppContext.BaseDirectory;

    /// <summary>Where Theseus keeps its board, settings, runs and logs, for About.</summary>
    public string DataFolder => App.DataDirectory;

    /// <summary>Opens the release page of the running version, which says what changed in it.</summary>
    public RelayCommand OpenReleaseNotesCommand { get; } = new(_ => Process.Start(new ProcessStartInfo(
        $"https://github.com/nigelbasel/Theseus/releases/tag/v{AppVersion.Current}") { UseShellExecute = true }));

    /// <summary>The same, with the build time spelled out.</summary>
    public string VersionDescribed => AppVersion.Described;

    /// <summary>The composer for a workstream that does not exist yet.</summary>
    public NewWorkstreamViewModel NewWorkstream { get; }

    /// <summary>Asks one workstream session to write a handoff and exit.</summary>
    public RelayCommand StopCardCommand { get; }

    /// <summary>Asks the ticked cards' running sessions to write a handoff and exit.</summary>
    public ReasonedCommand StopSelectedCommand { get; }

    /// <summary>Creates a verifier card for the one ticked card (verifier-card AC 9).</summary>
    public RelayCommand VerifySelectedWithHoldoutsCommand { get; }

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

    /// <summary>What the last action did, shown in the status bar. Setting it says a notice of information.</summary>
    public string Notice
    {
        get => _notice.Text;
        private set => SetNotice(NoticeSeverity.Information, value);
    }

    /// <summary>How serious the last notice is, which sets its glyph and colour in the status bar (baseline W46, C17).</summary>
    public NoticeSeverity NoticeSeverity => _notice.Severity;

    private void SetNotice(NoticeSeverity severity, string text)
    {
        string before = _notice.Text;
        NoticeSeverity was = _notice.Severity;

        _notice.Say(severity, text);

        if (!string.Equals(before, text, StringComparison.Ordinal))
        {
            Raise(nameof(Notice));
            Notices.Add(text);
        }

        if (was != severity)
        {
            Raise(nameof(NoticeSeverity));
        }
    }

    private void Fail(string text) => SetNotice(NoticeSeverity.Error, text);

    // Text added to the notice keeps the notice's severity, so a warning stays one when more is said after it.
    private void Extend(string text) => SetNotice(_notice.SeverityToExtend, text);

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

    /// <summary>Shows a warning in the status bar: something refused or missing, or the environment Theseus runs in (W46).</summary>
    public void Warn(string warning) => SetNotice(NoticeSeverity.Warning, warning);

    /// <summary>Shows news in the status bar, such as a report or a proposal arriving: information, not a warning.</summary>
    public void Say(string news) => Notice = news;

    /// <summary>Applies the remembered theme at startup.</summary>
    public void ApplyTheme() => ThemeApplier.Apply(_theme);

    /// <summary>Re-reads the plans, the board and the live sessions.</summary>
    public void Refresh()
    {
        _published = null;
        Timed("board", Board.Refresh);

        // A refresh replaces every card, so the new ones need telling where to report a tick. Wiring
        // this only when the grid changes left the selection count frozen at whatever it said before
        // the last refresh.
        WireLanes();
        Timed("sessions", Sessions.Refresh);
        Timed("lifecycles", SyncLifecycles);
        Timed("projects", WireProjects);

        // Again, now each card's project is set: sessions read it there, and a card that seemed in no project had its
        // proposals wait for the owner (Nigel, 2026-11-24).
        if (_published is { } published)
        {
            PublishSnapshot(published);
        }

        Timed("regroup", Board.Regroup);
        Timed("pull requests", SyncPullRequests);
        WireAdoption();

        // The strip is answered by what is on the board and which sessions are alive, and a refresh
        // changes both. Without this it would still name a session that had gone.
        Timed("stops", StopsChanged);
        Timed("account", () => SyncAccount());
        _stopRequests.Sweep(DateTimeOffset.UtcNow, _closed.IsFinished);
        Tells.Waiting.Sweep(DateTimeOffset.UtcNow, _ => true);
        LapseStartRequests();

        // Last, once lifecycles and the projects' running state are set, which its words read.
        Drawer.RefreshStart();

        // A turn that ends is what lets a waiting duty go, so it is looked at on every refresh, not only by its timer.
        TickDuties();
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
    /// Opens the one proposal window (sessions-run-the-project 0.2), from the banner, the Links pane, the project view or
    /// the proposals list: Approve makes the card, Reject… records the note and tells the proposer, Cancel decides nothing.
    /// </summary>
    public void ReviewProposal(Theseus.Core.Proposals.WorkstreamProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (Board.Card(proposal.RequesterKey) is not { } parent)
        {
            Warn($"{proposal.RequesterKey}, which proposed {proposal.Request.Title}, is no longer on the board.");

            return;
        }

        if (!proposal.IsPending)
        {
            Warn($"{proposal.Request.Title} is already {proposal.Status.ToString().ToLowerInvariant()}.");

            return;
        }

        ShowProposalWindow?.Invoke(new ProposalWindowViewModel(proposal, parent.Title, ReadBrief(proposal.Request.Brief),
            WhyNotApproveNow,
            each => Approve(each, startNow: false).Refusal,
            (each, note) => Chains?.Proposals?.Reject(each.Id, note) ?? "Theseus is not taking proposals."));
    }

    /// <summary>Opens the proposal window for a proposal by its id (a "Waits for you" line, 0.2).</summary>
    public void ReviewProposal(string proposalId)
    {
        if (Chains?.Proposals?.Find(proposalId) is { } proposal)
        {
            ReviewProposal(proposal);
        }
        else
        {
            Warn($"There is no proposal {proposalId}.");
        }
    }

    /// <summary>Shows a proposal's window; set by the app.</summary>
    public Action<ProposalWindowViewModel>? ShowProposalWindow { get; set; }

    /// <summary>
    /// Creates the card for a proposal its session approved when Nigel told it to, as Approve in the proposal window does
    /// (0.3), whether or not the composer is open: nothing typed there is touched.
    /// </summary>
    public Theseus.Core.Proposals.ProposalApproval ApproveFromSession(Theseus.Core.Proposals.WorkstreamProposal proposal, bool startNow)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        return Approve(proposal, startNow);
    }

    /// <summary>
    /// Makes the card a project's session proposed, without the owner's approval (sessions-run-the-project item 1): the
    /// same checks and card as an approval, kept in its history as created by that session, and nobody told.
    /// </summary>
    public Theseus.Core.Proposals.ProposalApproval CreateForSession(Theseus.Core.Proposals.WorkstreamProposal proposal, bool startNow)
    {
        ArgumentNullException.ThrowIfNull(proposal);

        if (Board.Card(proposal.RequesterKey) is not { Workstream.Project: { } project })
        {
            return Theseus.Core.Proposals.ProposalApproval.Refused($"{proposal.RequesterKey} is not in a project, so its proposals wait for the user.");
        }

        var made = Approve(proposal, startNow, bySession: true);

        // The card is made before its row is written, so a row that could not be written leaves it ad hoc: say so.
        return made.Key is { } key && !string.Equals(Board.Card(key)?.Workstream.Project, project, StringComparison.OrdinalIgnoreCase)
            ? made with { Shortfall = string.IsNullOrWhiteSpace(Notice) ? $"its row was not written, so it is not in {project}" : Notice }
            : made;
    }

    private Theseus.Core.Proposals.ProposalApproval Approve(Theseus.Core.Proposals.WorkstreamProposal proposal, bool startNow,
        bool bySession = false)
    {
        // The window is not modal and a session may approve or withdraw meanwhile, so the book is asked again here, on the
        // board's thread, rather than trusting the proposal as it was when the window opened.
        if (Chains?.Proposals?.Find(proposal.Id) is not { IsPending: true } current)
        {
            return Theseus.Core.Proposals.ProposalApproval.Refused(Chains?.Proposals?.Find(proposal.Id) is { } decided
                ? $"{proposal.Request.Title} is already {decided.Status.ToString().ToLowerInvariant()}."
                : $"There is no proposal {proposal.Id}.");
        }

        proposal = current;

        if (Board.Card(proposal.RequesterKey) is not { } parent)
        {
            return Theseus.Core.Proposals.ProposalApproval.Refused($"{proposal.RequesterKey} is no longer on the board.");
        }

        if (WhyCannotApprove(proposal) is { } cannot)
        {
            return Theseus.Core.Proposals.ProposalApproval.Refused($"It cannot be approved yet: {cannot}");
        }

        return NewWorkstream.CreateFromProposal(parent.Workstream, proposal, startNow, bySession);
    }

    private string? WhyNotApproveNow(Theseus.Core.Proposals.WorkstreamProposal proposal) =>
        Board.Card(proposal.RequesterKey) is not { } parent
            ? $"{proposal.RequesterKey} is no longer on the board"
            : !System.IO.Directory.Exists(parent.Workstream.WorkingDirectory)
                ? $"{parent.Workstream.WorkingDirectory}, {parent.Title}'s working directory, is not there"
                : WhyCannotApprove(proposal);

    private static string? ReadBrief(string path)
    {
        try
        {
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
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

    // A selected project stops as its own ■ does: each of its cards with a session open (1.1d AC 43).
    private IReadOnlyList<StopStep> ChosenSteps() =>
        StepsFor(Runner.StopSelected(Chosen(), Board.ChosenProjects));

    /// <summary>
    /// Ticks every card shown whose session is running, and unticks the rest of them, the last column's
    /// included: a tick the user can see and remove is honest where a silent exclusion is not. A card
    /// behind its project card is not shown, so it is left alone (PR 2 review, item 1).
    /// </summary>
    private void SelectLive()
    {
        Board.SelectLiveShown();
        SelectionMoved();
    }

    private void FoldEvery()
    {
        Board.FoldAll();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private bool CanVerify(WorkstreamCardViewModel card) => WhyCannotVerify(card) is null;

    private IReadOnlyDictionary<string, IReadOnlyList<PieceCard>> _standingVerifiers =
        new Dictionary<string, IReadOnlyList<PieceCard>>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Says in a dialog why something pressed cannot be done, with a title that names what could not be done (baseline
    /// A5), or in the status bar when no dialog is set. Set by the app.
    /// </summary>
    public Action<string, string>? ExplainRefusal { get; set; }

    // A refusal only in the status line read as "nothing happens" (Nigel, 2026-11-23).
    private void Refuse(string title, string text)
    {
        if (ExplainRefusal is { } explain)
        {
            explain(title, text);
        }
        else
        {
            Warn(text);
        }
    }

    // The project view's refusals come through ProjectViewCommands without a title, so they get a general one.
    private void Refuse(string text) => Refuse("Can't do that yet", text);

    /// <summary>The card menu's Verify tooltip when it can run.</summary>
    private const string VerifyToolTipWhenItCan =
        "Create a verifier card that runs the holdout scenarios against this card's change and reports only a verdict";

    /// <summary>A card's holdout folder: the one chosen in Options, else its project's default (1.1e AC 6c).</summary>
    private HoldoutLocation HoldoutOf(WorkstreamCardViewModel card) => _holdouts.LocationOf(card.Workstream);

    /// <summary>Why Independent check cannot run for a card, or null (1.1e AC 6a, 6c), from the folders as last refreshed.</summary>
    private string? WhyCannotVerify(WorkstreamCardViewModel card) =>
        card.Workstream is { IsArchived: false, Role: WorkstreamRole.None } && PendingVerifiersOf(card) is [_, ..] verifiers
            ? _standingVerifiers.GetValueOrDefault(card.Key, []).FirstOrDefault(VerifierCards.NeedsAStart) is { } idle
                ? $"{idle.Title} is already made to check it, but is not running: Start it again from the Waits line"
                : VerifierCards.WhyAlreadyChecked(verifiers)
            : _holdouts.WhyCannotVerify(card.Workstream, card.Title);

    /// <summary>
    /// The standing verifier cards made for a card, by title: it gets no second (Nigel, 2026-11-23). Read from the map made
    /// at each refresh, since this is asked by CanExecute on every requery (review, finding 3).
    /// </summary>
    private List<string> PendingVerifiersOf(WorkstreamCardViewModel card) =>
        [.. _standingVerifiers.GetValueOrDefault(card.Key, []).Select(verifier => verifier.Title)];

    /// <summary>Works out again which verifier cards stand, by the card each checks, and each card's Verify texts.</summary>
    private void RefreshStandingVerifiers()
    {
        _standingVerifiers = VerifierCards.BySubject(
            Board.Cards.Where(card => card.Workstream.Role == WorkstreamRole.Verifier).Select(card =>
                new PieceCard(card.Key, card.Title, card.ColorHex, card.Lifecycle, Role: WorkstreamRole.Verifier)
                {
                    IsArchived = card.Workstream.IsArchived,
                    HasRun = HasRun(card),
                    StartFailure = _verifierStartFailures.GetValueOrDefault(card.Key),
                    IsStarting = _launcher.InFlight.IsInFlight(card.Key),
                    IsHeadless = Headless?.Holds(card.Key) == true,
                    IsObsolete = IsObsolete(card),
                }),
            Chains?.AllLinks ?? []);
    }

    private static bool HasRun(WorkstreamCardViewModel card) => card.LastStartedUtc is not null || card.Workstream.LastSessionId is not null;

    private void SetVerifyTexts(WorkstreamCardViewModel card)
    {
        var why = WhyCannotVerify(card);
        card.VerifyToolTip = why ?? VerifyToolTipWhenItCan;
        card.VerifyOffReason = why;
    }

    /// <summary>
    /// The way on from a disabled view button, offered beside it (1.1e AC 6a, 6c) as a button, since it acts: a link only
    /// navigates (baseline W15, finding A10).
    /// </summary>
    private PieceActionViewModel? WayOn(WorkstreamCardViewModel card, StageAction action) =>
        action != StageAction.VerifyWithHoldouts || PendingVerifiersOf(card).Count > 0
            ? null
            : _holdouts.WayOn(card.Workstream) switch
            {
                HoldoutWayOn.SetFolder => new PieceActionViewModel("Set the holdout folder…",
                    () => OpenProjectHoldouts(card.Workstream.Project ?? string.Empty),
                    toolTip: "Open the project's Project properties at its holdout folder."),
                HoldoutWayOn.WriteScenarios => new PieceActionViewModel("Write scenarios…", () => WriteScenarios(card),
                    toolTip: $"Create a card that writes the holdout scenarios in {HoldoutOf(card).Folder}; it starts when you start it."),
                _ => null,
            };

    /// <summary>Why a view button for a card cannot run now, or null (1.1e AC 6a): its tooltip while it is disabled.</summary>
    private string? WhyNot(WorkstreamCardViewModel card, StageAction action) => action switch
    {
        StageAction.VerifyWithHoldouts => WhyCannotVerify(card),
        StageAction.Start when !CanStartCard(card) => _launcher.Impediment
                                                      ?? (card.IsArchived ? $"{card.Title} is archived" : card.IsLive
                                                          ? $"{card.Title} is already running"
                                                          : $"{card.Title} cannot be started from the board: it has no prompt"),
        StageAction.Stop when CardSteps(card).Count == 0 => $"{card.Title} has no session to stop",
        StageAction.Focus when !CanFocusCard(card) => $"{card.Title} has no terminal to bring forward",
        _ => null,
    };

    /// <summary>
    /// Finds each project's holdout folder (1.1e AC 6e) and writes those in force to holdout-folders.json, which the
    /// holdout guard reads, so the board and the guard always agree.
    /// </summary>
    private void RefreshHoldouts(IReadOnlyList<Workstream> cards)
    {
        _holdouts.Refresh(_settings.Read().ProjectHoldoutFolders, HoldoutConventionOf, cards);

        if (HoldoutFolderFile.Write(HoldoutFolderFile.PathIn(), _holdouts.GuardedEntries))
        {
            TheseusLog.Information("Wrote the holdout folders in force: {Folders}",
                string.Join("; ", _holdouts.Entries.Select(entry => $"{entry.Project}: {entry.Folder} ({entry.HowFound})")));
        }

        if (Settings is not null)
        {
            Settings.HoldoutEntries = _holdouts.Entries;
        }
    }

    /// <summary>
    /// What a project's holdout folder is found from: the clone of its home code repo (the first in its Repos table, found
    /// among the folders its cards work in, the worktrees folder, then the paths its records name), and the holdout
    /// repository its project.md records. A clone once found is kept, so the records are read again only if it goes.
    /// </summary>
    private HoldoutConvention HoldoutConventionOf(string project)
    {
        var reading = _projectReads.Current.FirstOrDefault(each => string.Equals(each.Name, project, StringComparison.OrdinalIgnoreCase));
        var members = _projectReads.History.CardsOf(project);
        // Only cards that build: a verifier, scenario writer or updater works in or beside a holdout (review of PR 7, 4).
        var folders = Board.Cards
            .Where(card => card.Workstream.Role == WorkstreamRole.None
                           && (members.Contains(card.Key) || string.Equals(card.Workstream.Project, project, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(card => new[] { card.Workstream.WorkingDirectory }.Concat(card.Workstream.AddDirectories));

        if (_homeClones.TryGetValue(project, out var known) && Directory.Exists(known)
            && string.Equals(Path.GetFileName(known), HomeRepo.NameIn(reading?.Text), StringComparison.OrdinalIgnoreCase))
        {
            return new HoldoutConvention(known, HomeRepo.RecordedHoldoutIn(reading?.Text));
        }

        var worktrees = _settings.Read().WorktreesFolder;
        var convention = HoldoutConvention.For(reading?.Text, folders, worktrees, () => RecordTexts(reading?.Folder), out var foundBy);
        var line = convention.HomeClone ?? "none";

        if (!_homeCloneLines.TryGetValue(project, out var said) || said != line)
        {
            _homeCloneLines[project] = line;
            TheseusLog.Information("Holdout convention for {Project}: home clone {Clone} found via {FoundBy} (worktrees folder '{Worktrees}', recorded {Recorded})",
                project, convention.HomeClone ?? "none", foundBy, worktrees, convention.Recorded is null ? "none" : convention.Recorded.Name ?? "<repo>-holdout");
        }

        if (convention.HomeClone is { } clone)
        {
            _homeClones[project] = clone;
        }

        return convention;
    }

    private readonly Dictionary<string, string> _homeClones = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _homeCloneLines = new(StringComparer.OrdinalIgnoreCase);

    // The project's records beside project.md: the Markdown files in each piece's folder under work.
    private static IEnumerable<string> RecordTexts(string? projectFolder)
    {
        var work = projectFolder is null ? null : Path.Combine(projectFolder, "work");

        if (work is null || !Directory.Exists(work))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateDirectories(work).SelectMany(piece => Directory.EnumerateFiles(piece, "*.md")))
        {
            string text;

            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            yield return text;
        }
    }

    /// <summary>Opens a project's Project properties at its Holdouts tab (options-made-clear item 1), by folder name.</summary>
    private void OpenProjectHoldouts(string project)
    {
        if (Board.ProjectCards.FirstOrDefault(each => string.Equals(each.Name, project, StringComparison.OrdinalIgnoreCase)) is { } card)
        {
            ShowProjectProperties(card, ProjectPropertiesViewModel.HoldoutsTab);
        }
    }

    /// <summary>
    /// Write scenarios… (1.1e AC 6c): creates a scenario-writing card for the card's project, in its holdout folder, made on
    /// first use. It starts nothing.
    /// </summary>
    /// <param name="card">The build whose project needs scenarios.</param>
    /// <param name="quietly">Whether Theseus is doing it itself, so a refused start opens no dialog nobody asked for.</param>
    private void WriteScenarios(WorkstreamCardViewModel card, bool quietly = false)
    {
        var holdout = HoldoutOf(card);

        if (holdout.IsDefault && !holdout.Ensure())
        {
            Warn($"{holdout.Folder} could not be made a git repository (is git installed?); the scenarios there will have no history.");
        }
        var project = card.Workstream.Project;
        var writer = WriterFor(project, card.Title, holdout.Folder);
        var key = WorkstreamKey.Unique(writer.Title, [.. _plans.Keys(), .. Chains?.TakenKeys ?? []]);

        _plans.Write(key, new SessionPlan(_plans.Keys().Count + 1, writer.Title, card.Workstream.ColorHex,
            writer.WorkingDirectory, writer.AddDirectories, null, DateTimeOffset.Now.ToString("s"), writer.Prompt));

        Refresh();
        Board.Place(key, card.Column, card.Workstream.Area);

        if (Board.Card(key) is not { } created)
        {
            Warn($"{writer.Title} was written but is not on the board; refresh and try again.");

            return;
        }

        // Its project is kept on the card, so each start aims it at the project's holdout folder of that day (item 7b).
        created.Workstream = created.Workstream with { Role = ScenarioWriterCard.Role, Project = project };
        Board.Persist(created);
        TheseusLog.Information("Created {Writer} to write holdout scenarios in {Folder}", key, holdout.Folder);

        // Theseus starts what it makes; nothing is left for the owner to start (Nigel, 2026-11-23).
        StartMany([created.Workstream], explain: !quietly);
    }

    /// <summary>A project's scenario writer, aimed at a holdout folder, given the project's slugs for its Checks lines.</summary>
    private ScenarioWriterCard WriterFor(string? project, string fallbackTitle, string folder)
    {
        var reading = project is null ? null : Readable(project);

        return ScenarioWriterCard.For(reading?.File?.Title ?? fallbackTitle, folder, reading?.Folder,
            reading?.File is { } file ? ScenarioWriterCard.BuildSlugs(file.Workstreams, key => Board.Card(key)?.Workstream.Role) : null);
    }

    /// <summary>
    /// A scenario writer's project: the one kept on its card, else the project whose home it has in reach, as older writers
    /// were made with none (git-22, item 7a).
    /// </summary>
    private string? WriterProject(Workstream writer) => RoleProject.Of(writer, _projectReads.Current);

    /// <summary>
    /// Aims a scenario writer at its project's holdout folder of today before it starts, rewriting its plan when the folder
    /// has changed: a folder saved when the card was made sent old writers to the wrong place (git-22, item 7b).
    /// </summary>
    private Workstream AimedWriter(Workstream writer)
    {
        if (writer.Role != WorkstreamRole.ScenarioWriter || WriterProject(writer) is not { } project)
        {
            return writer;
        }

        var folder = _holdouts.LocationOf(writer with { Project = project }).Folder;

        if (string.IsNullOrWhiteSpace(folder) || SameFolder(writer.WorkingDirectory, folder))
        {
            return writer;
        }

        var aimed = WriterFor(project, writer.Title, folder);

        if (_plans.TryRead(writer.Key) is { } plan)
        {
            _plans.Write(writer.Key, plan with { Cwd = aimed.WorkingDirectory, AddDirs = aimed.AddDirectories, Prompt = aimed.Prompt });
        }

        var updated = writer with { WorkingDirectory = aimed.WorkingDirectory, AddDirectories = aimed.AddDirectories, Prompt = aimed.Prompt, Project = project };

        if (Board.Card(writer.Key) is { } card)
        {
            card.Workstream = updated;
            Board.Persist(card);
        }

        TheseusLog.Information("Aimed {Writer} at {Folder}, its project's holdout folder now, before starting it", writer.Key, folder);

        return updated;
    }

    /// <summary>Independent check…: makes this card's verifier and starts it (Nigel, 2026-11-23).</summary>
    private void VerifyWithHoldouts(WorkstreamCardViewModel card)
    {
        if (MakeVerifier(card) is { } made)
        {
            StartVerifier(made);
        }
    }

    /// <summary>
    /// Makes a verifier card for this card, or says why not: a follow-on in the checked card's folder with the holdout clone
    /// in reach, role verifier, linked both ways, with its fixed brief (verifier-card AC 7, 9 and 10). It starts nothing.
    /// </summary>
    /// <param name="card">The build.</param>
    /// <param name="quietly">Whether a refusal is only logged: Theseus making one itself opens no dialog nobody asked for.</param>
    private WorkstreamCardViewModel? MakeVerifier(WorkstreamCardViewModel card, bool quietly = false)
    {
        // Worked out at the press, not taken from the last refresh, so a second press never makes a second verifier for the
        // same build (git-22, 2026-11-23).
        RefreshStandingVerifiers();

        if (WhyCannotVerify(card) is { } reason)
        {
            if (quietly)
            {
                TheseusLog.Information("Made no verifier for {Workstream} itself: {Reason}", card.Key, reason);
            }
            else
            {
                Refuse($"Can't make a verifier for {card.Title} yet", $"{reason}.");
            }

            return null;
        }

        var holdout = HoldoutOf(card).Folder;

        var verifier = VerifierCard.For(card.Workstream, holdout, DateTimeOffset.Now, RecordsFolder(card.Workstream));
        var key = WorkstreamKey.Unique(verifier.Title, [.. _plans.Keys(), .. Chains?.TakenKeys ?? []]);

        _plans.Write(key, new SessionPlan(_plans.Keys().Count + 1, verifier.Title, card.Workstream.ColorHex,
            verifier.WorkingDirectory, verifier.AddDirectories, null, DateTimeOffset.Now.ToString("s"), verifier.Prompt));

        Refresh();
        Board.Place(key, card.Column, card.Workstream.Area);

        // Without the card there is no role to give it, and a card with no role must not be linked as a verifier
        // (verifier-card attempt 1, C10).
        if (Board.Card(key) is not { } created)
        {
            Warn($"{verifier.Title} was written but is not on the board, so it was not made a verifier; refresh and try again.");
            TheseusLog.Warning("Created {Verifier} but could not find its card to make it a verifier", key);

            return null;
        }

        created.Workstream = verifier.Apply(created.Workstream);
        Board.Persist(created);

        Chains?.FollowedOn(card.Key, key, verifier.ReportPath);
        RefreshStandingVerifiers();
        SetVerifyTexts(card);
        WriteVerifierRow(card, created);

        WireProjectChoices();

        TheseusLog.Information("Created {Verifier} to verify {Workstream} with the holdout scenarios", key, card.Key);

        return created;
    }

    /// <summary>
    /// Starts a verifier Theseus has just made, since the owner no longer presses Start for one (Nigel, 2026-11-23). A start
    /// that fails is remembered with its reason, which the project view shows with Start again.
    /// </summary>
    private void StartVerifier(WorkstreamCardViewModel verifier) => StartVerifiers([verifier]);

    /// <summary>
    /// Starts verifiers Theseus has made, in one go, so they give one notice for them all. A start that
    /// fails is remembered with its reason for the Waits line's Start again; one that fails later, in its tab, shows there
    /// too, since a standing verifier neither running nor starting always does.
    /// </summary>
    /// <param name="verifiers">The verifiers to start together.</param>
    /// <param name="quietly">Whether Theseus is starting them itself, so a refusal opens no dialog nobody asked for.</param>
    private void StartVerifiers(IReadOnlyList<WorkstreamCardViewModel> verifiers, bool quietly = false)
    {
        var before = verifiers.ToDictionary(verifier => verifier.Key, verifier => verifier.Workstream.LastSessionId, StringComparer.OrdinalIgnoreCase);

        foreach (var verifier in verifiers)
        {
            _verifierStartFailures.Remove(verifier.Key);
        }

        StartMany([.. verifiers.Select(verifier => verifier.Workstream)], explain: !quietly);

        var unstarted = verifiers.Where(verifier => Board.Card(verifier.Key) is not { } after || after.Workstream.LastSessionId == before[verifier.Key]).ToList();

        if (unstarted.Count == 0)
        {
            return;
        }

        var why = _launcher.Impediment ?? _launcher.LastRefusal ?? "it could not be started";

        foreach (var verifier in unstarted)
        {
            _verifierStartFailures[verifier.Key] = why;
            TheseusLog.Warning("Made {Verifier} but could not start it: {Why}", verifier.Key, why);
        }

        RefreshStandingVerifiers();
        WireProjectChoices();
    }

    /// <summary>
    /// Independent check… for several builds, as Theseus starts them itself: each verifier is made, then all are started
    /// together, so their notices do not overwrite one another.
    /// </summary>

    private void VerifyManyWithHoldouts(IReadOnlyList<string> keys, bool quietly)
    {
        var made = keys.Select(Board.Card).OfType<WorkstreamCardViewModel>()
            .Select(card => MakeVerifier(card, quietly)).OfType<WorkstreamCardViewModel>().ToList();

        if (made.Count > 0)
        {
            StartVerifiers(made, quietly);
        }
    }

    /// <summary>A start that never reached its tab: said on the board, and remembered for a verifier, which waits for nobody else.</summary>
    public void StartLost(string key, string text)
    {
        Warn(text);

        if (Board.Card(key) is { Workstream.Role: WorkstreamRole.Verifier })
        {
            _verifierStartFailures[key] = "Windows Terminal opened the tab but never ran the session in it; close that tab";
            RefreshStandingVerifiers();
            WireProjectChoices();
        }
    }

    /// <summary>A project workstream's records folder in its project home, as last read; null for any other card.</summary>
    private string? RecordsFolder(Workstream workstream) =>
        workstream is { Project: { } project, Slug: { } slug }
        && _projectReads.Current.FirstOrDefault(reading =>
            reading.IsReadable && string.Equals(reading.Name, project, StringComparison.OrdinalIgnoreCase)) is { Folder: { } home }
            ? Path.Combine(home, WorkstreamArtefactReader.WorkFolder, slug)
            : null;

    /// <summary>Whether a projects folder is set, so New project… shows in the File menu and on the toolbar (1.1c AC 6).</summary>
    public bool HasProjectsFolder => _hasProjectsFolder;

    /// <summary>The first-run setup: at startup while no projects folder is set, from Tools › Set up Theseus…, and from New project.</summary>
    public FirstRunViewModel FirstRun { get; }

    /// <summary>New project…: a project home from the starter, with what the owner gives (1.1b AC 7; 1.1c AC 6).</summary>
    public RelayCommand NewProjectCommand { get; }

    /// <summary>Asks for a new project's details, starting from a name and title; null when cancelled. Set by the app.</summary>
    public Func<string, string, NewProjectDetails?>? AskNewProject { get; set; }

    /// <summary>Asks for one line of text: the dialog's title, the field's label, and what it starts with. Set by the app.</summary>
    public Func<string, string, string, string?>? AskText { get; set; }

    /// <summary>Asks for a new objective. Set by the app.</summary>
    public Func<NewObjective?>? AskObjective { get; set; }

    /// <summary>
    /// Asks which project, objective and slug a card joins, from the readable projects, with one project chosen first
    /// when asked from its view (1.1e AC 6). Set by the app.
    /// </summary>
    public Func<IReadOnlyList<ProjectOption>, string, string?, AddToProjectChoice?>? AskAddToProject { get; set; }

    private void NewProject()
    {
        if (AskNewProject?.Invoke(string.Empty, string.Empty) is { } details && StartProject(details) is { } home)
        {
            StartLead(details.Name, details.Title, home);
        }
    }

    // Theseus starts what it makes (Nigel, 2026-11-23): a project made empty has no card for its Start to start.
    private void StartLead(string project, string title, string home)
    {
        var lead = ProjectLeadCard.For(title.Length > 0 ? title : project, home);
        var key = WorkstreamKey.Unique(lead.Title, [.. _plans.Keys(), .. Chains?.TakenKeys ?? []]);

        _plans.Write(key, new SessionPlan(_plans.Keys().Count + 1, lead.Title, ProjectCardViewModel.NoColour, lead.WorkingDirectory, [],
            null, DateTimeOffset.Now.ToString("s"), lead.Prompt));
        Refresh();

        if (Board.Card(key) is not { } card)
        {
            Warn($"Project {project} was started, but its lead card {lead.Title} is not on the board; refresh and try again.");

            return;
        }

        try
        {
            AddFirstRow(home, project, card);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectEditRefusedException)
        {
            Fail($"Project {project} was started, but {lead.Title} could not be added to it: {exception.Message}");

            return;
        }

        TheseusLog.Information("Created {Lead} to lead project {Project}", key, project);
        Notice = $"Started project {project} in {home}, with {lead.Title} as its lead, starting now.";
        StartMany([card.Workstream]);
    }

    /// <summary>Adds a card as the first row of a new project's first objective, and returns its records folder.</summary>
    private string AddFirstRow(string home, string project, WorkstreamCardViewModel card)
    {
        var text = File.ReadAllText(Path.Combine(home, ProjectCatalog.ProjectFileName));
        var file = ProjectFileReader.Read(project, text).File
                   ?? throw new ProjectEditRefusedException("the new project.md does not read");
        var first = file.Objectives.FirstOrDefault()
                    ?? throw new ProjectEditRefusedException("the new project.md has no objective");
        var row = new ProjectWorkstreamRow(card.Key, project, BranchFor(file, project), ReposOf(card), 0);
        var reading = ProjectFileReading.Read(file) with { Folder = home, Text = text };

        ProjectFileWriter.Apply(reading, current => ProjectFileEditor.AddRow(current, first.Number, row));

        var records = Path.Combine(home, WorkstreamArtefactReader.WorkFolder, project);
        Directory.CreateDirectory(records);
        _projectReads.MarkStale();
        WireProjects();

        return records;
    }

    /// <summary>Starts a project from the starter and returns its reading-to-be's folder, or null with a notice.</summary>
    private string? StartProject(NewProjectDetails details)
    {
        var folder = ProjectsFolderPath.Normalize(_settings.Read().ProjectsFolder);

        if (ProjectStarter.Refusal(folder, details.Name) is { } refusal)
        {
            Warn(refusal);

            return null;
        }

        try
        {
            var home = ProjectStarter.Start(folder, details);
            TheseusLog.Information("Started project {Project} in {Folder}", details.Name, home);
            Notice = $"Started project {details.Name} in {home}. Its project card is on the board.";
            _projectReads.MarkStale();
            WireProjects();

            return home;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Fail($"Project {details.Name} could not be started: {exception.Message}");

            return null;
        }
    }

    /// <summary>The project as last read, by name, when it reads.</summary>
    private ProjectFileReading? Readable(string project) =>
        _projectReads.Current.FirstOrDefault(reading =>
            reading.IsReadable && string.Equals(reading.Name, project, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Writes an owner's edit to a project's file (1.1c AC 9) and reads the projects again; a refusal, such as the file
    /// having changed since it was read, is the notice. Returns whether it was written.
    /// </summary>
    /// <summary>
    /// Applies a project tool's edit to a project's file on the UI thread, as the owner's own edits are made, so the two
    /// are one at a time and a second edit is checked against the file as just written (1.1g review of PR 2, finding 2).
    /// Throws <see cref="ProjectEditRefusedException"/> when it is refused.
    /// </summary>
    public string WriteProjectFile(string project, Func<string, string> edit) =>
        Write(Readable(project) ?? throw new ProjectEditRefusedException($"{project} does not read"), edit);

    private string Write(ProjectFileReading reading, Func<string, string> edit)
    {
        var written = ProjectFileWriter.Apply(reading, edit);
        _projectReads.MarkStale();
        _projectReads.Replace(ProjectFileReader.Read(reading.Name, written) with
        {
            Folder = reading.Folder,
            Text = written,
            Artefacts = reading.Artefacts,
        });

        return written;
    }

    private bool EditProject(string project, Func<string, string> edit, string done)
    {
        if (Readable(project) is not { } reading)
        {
            Warn($"{project} does not read, so Theseus cannot edit it; open its project.md instead.");

            return false;
        }

        try
        {
            Write(reading, edit);
            TheseusLog.Information("Edited the project file of {Project}: {Edit}", project, done);
            Notice = done;

            return true;
        }
        catch (ProjectEditRefusedException refused)
        {
            Warn($"{project}: {refused.Message}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Fail($"{project}: project.md could not be written: {exception.Message}");
        }

        return false;
    }

    /// <summary>A branch for a slug, named as the project's primary branch is: its owner's prefix, then the slug.</summary>
    private static string BranchFor(ProjectFile file, string slug) => AddToProjectFlow.BranchFor(file, slug);

    /// <summary>The Repos cell for a card's row: its folders' names, which a session corrects as it works.</summary>
    private static string ReposOf(WorkstreamCardViewModel card) =>
        string.Join(", ", new[] { card.Workstream.WorkingDirectory }.Concat(card.Workstream.AddDirectories)
            .Select(folder => Path.GetFileName(folder.TrimEnd('\\', '/')))
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)) is { Length: > 0 } names ? names : "[to be recorded]";

    /// <summary>
    /// Add to project… on a card (1.1c AC 7), or on a box of a project's view with that project chosen (1.1e AC 6): adds
    /// its row to the chosen objective's table.
    /// </summary>
    private void AddToProject(WorkstreamCardViewModel card, string? project = null)
    {
        var addition = AddToProjectFlow.Ask(_projectReads.Current, card.Key, card.Title, ReposOf(card), project, AskAddToProject,
            out var notice);

        if (notice is not null)
        {
            Notice = notice;
        }

        if (addition is not null)
        {
            EditProject(addition.Project, text => ProjectFileEditor.AddRow(text, addition.Objective, addition.Row), addition.Done);
        }
    }

    /// <summary>
    /// A card made from a proposal by a card in a project joins that project (1.1e AC 31): its row goes under the objective
    /// the proposal names, with its records folder name, branch and repos (none named: branch none, repos read-only),
    /// written as Add to project… writes it and refused if the file changed underneath. A proposal from an ad hoc card
    /// makes an ad hoc card.
    /// </summary>
    private string? JoinProposersProject(WorkstreamCardViewModel card, Theseus.Core.Proposals.WorkstreamProposal proposal)
    {
        if (Board.Card(proposal.RequesterKey) is not { Workstream.Project: { } project } || Readable(project)?.File is not { } file)
        {
            return null;
        }

        if (Theseus.Core.Proposals.ProposalRow.For(file, proposal.Request, card.Key) is not { } placed)
        {
            return $"Created {card.Title}, not in {project}: "
                   + (Theseus.Core.Proposals.ProposalRow.WhyNot(file, proposal.Request) ?? "it has no records folder name; add it with Add to project….");
        }

        if (!EditProject(project, text => ProjectFileEditor.AddRow(text, placed.Objective, placed.Row),
                $"Created {card.Title}, in {project} as {placed.Row.Slug} under objective {placed.Objective}."))
        {
            return $"Created {card.Title}, not in {project}: {Notice}";
        }

        // In the project now, not when the read lands, so a Start now opens it in the project's window.
        card.Workstream = card.Workstream with { Project = project, Slug = placed.Row.Slug };

        return Notice;
    }

    /// <summary>
    /// Why a proposal cannot be approved yet, or null (1.1e AC 31): one from a card in a project must name an objective of
    /// its file, and the file must read.
    /// </summary>
    private string? WhyCannotApprove(Theseus.Core.Proposals.WorkstreamProposal proposal) =>
        Board.Card(proposal.RequesterKey) is not { Workstream.Project: { } project }
            ? null
            : Readable(project) is { File: { } file } reading
                ? Theseus.Core.Proposals.ProposalRow.WhyNot(file, proposal.Request)
                  ?? (ProjectFileWriter.IsCurrent(reading) ? null : $"{project}'s project.md changed since Theseus read it; wait a moment and try again.")
                : $"{project}'s project.md does not read, so its row cannot be written. Update it first.";

    /// <summary>
    /// A verifier's row (1.1e AC 31): under the checked card's objective, its records folder verify-&lt;slug&gt;, branch none
    /// and repos read-only.
    /// </summary>
    private void WriteVerifierRow(WorkstreamCardViewModel checkedCard, WorkstreamCardViewModel verifier)
    {
        if (checkedCard.Workstream is not { Project: { } project, Slug: { } slug }
            || Readable(project)?.File is not { } file
            || file.ObjectiveOf(slug) is not { } objective)
        {
            return;
        }

        var row = Theseus.Core.Proposals.ProposalRow.ForVerifier(file, slug, verifier.Key);
        var name = row.Slug;

        if (EditProject(project, text => ProjectFileEditor.AddRow(text, objective.Number, row),
                $"Created {verifier.Title}, in {project} as {name} under objective {objective.Number}. Start it when you are ready."))
        {
            verifier.Workstream = verifier.Workstream with { Project = project, Slug = name };
            Board.Persist(verifier);
        }
    }

    /// <summary>Remove from project on a card (1.1c AC 7): removes its rows; its records stay.</summary>
    private void RemoveFromProject(WorkstreamCardViewModel card)
    {
        if (card.Workstream.Project is not { } project)
        {
            return;
        }

        EditProject(project, text => ProjectFileEditor.RemoveRows(text, card.Key),
            $"{card.Title} is no longer in {project}. Its records in the project's work folder are kept.");
    }

    /// <summary>
    /// Convert to project… on a card (1.1c AC 7): a new project named after the card, the card as the first row of its
    /// first objective, and its handoff copied in as the project's first record.
    /// </summary>
    private void ConvertToProject(WorkstreamCardViewModel card)
    {
        var name = ProjectMembership.IsSlug(card.Key) ? card.Key : string.Empty;

        if (AskNewProject?.Invoke(name, card.Title) is not { } details || StartProject(details) is not { } home)
        {
            return;
        }

        try
        {
            var records = AddFirstRow(home, details.Name, card);
            var copied = string.Empty;

            if (card.Workstream.StatusDocumentPath is { } handoff && File.Exists(handoff))
            {
                try
                {
                    File.Copy(handoff, Path.Combine(records, "handoff.md"), overwrite: false);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    copied = $" Its handoff could not be copied in: {exception.Message}";
                }
            }

            Notice = $"{card.Title} is now project {details.Name}, as its first workstream. Fill in its project.md.{copied}";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ProjectEditRefusedException)
        {
            Fail($"Project {details.Name} was started, but {card.Title} could not be added to it: {exception.Message}");
        }
    }

    /// <summary>Add objective… on a project card (1.1c AC 8), adding to a phase as a phase heading's does (1.1d AC 49).</summary>
    private void AddObjective(string project, int phase) => EditsOf(project).AddObjective?.Invoke(phase);

    /// <summary>
    /// The project view's edits on a project's file: ticks, Rename objective… and Add objective…, each asking as it must and
    /// written by <see cref="EditProject"/>.
    /// </summary>
    private ProjectViewEdits EditsOf(string project) =>
        ProjectViewFileEdits.For(project, EditProject, () => AskObjective?.Invoke(),
            (title, prompt, current) => AskText?.Invoke(title, prompt, current),
            () =>
            {
                _redrawProjectView = true;
                WireProjectChoices();
            });

    /// <summary>
    /// Reads the projects in the projects folder; the cards they name are put in them when the read lands. With no folder
    /// set nothing runs and nothing on the board changes (project-view AC 22; 1.1b AC 1 and 13; 1.1c AC 1).
    /// </summary>
    private void WireProjects()
    {
        var folder = ProjectsFolderPath.Normalize(_settings.Read().ProjectsFolder);

        if (_hasProjectsFolder != !string.IsNullOrWhiteSpace(folder))
        {
            _hasProjectsFolder = !_hasProjectsFolder;
            Raise(nameof(HasProjectsFolder));
        }

        // The watcher asks for a read when a file changes. A folder it has only just started watching is read, since
        // nothing told it of changes while unwatched; one it cannot watch (not there, or gone) is read on every refresh,
        // so it shows as unreadable and is found again when it comes back.
        var watch = _projectWatcher.Watch(folder);

        if (watch == ProjectFolderWatch.Started || (watch == ProjectFolderWatch.NotWatching && !string.IsNullOrWhiteSpace(folder)))
        {
            _projectReads.MarkStale();
        }

        // The read runs off this thread and WireProjectChoices follows when it lands.
        _projectReads.Request(folder);
        WireProjectChoices();
    }

    /// <summary>
    /// Puts each card in the project whose file names it, as last read (1.1c AC 1 and 2), then lays out the project cards.
    /// Run after every refresh, since a card rebuilt from its files knows no project.
    /// </summary>
    private void WireProjectChoices()
    {
        var readings = _projectReads.Current;

        // Logged when what is read changes, not on every refresh, so an unreadable project is in the log once.
        var summary = string.Join("; ", readings.Select(reading => reading.Describe));

        if (!string.Equals(summary, _projectSummary, StringComparison.Ordinal))
        {
            _projectSummary = summary;

            foreach (var reading in readings.Where(reading => !reading.IsReadable))
            {
                TheseusLog.Warning("Project {Project} in {Folder} is {Reading}", reading.Name, reading.Folder, reading.Describe);
            }
        }

        var memberOf = ApplyMembership(_projectReads.Members);

        // Before this run's first read lands there is no project card, so nothing may leave the grid: remembered members
        // only choose the window a start opens in (1.1c review, item 2).
        WireProjectBoard(readings, memberOf, _projectReads.HasLanded && _projectReads.Members.ByCard.Count > 0);
    }

    /// <summary>
    /// A card's current piece of work, as the project files last read say, for the hand-over a link records (1.1f D15).
    /// Read from any thread: the members are replaced whole, never changed in place.
    /// </summary>
    public string? CurrentPieceOf(string cardKey) => _projectReads.Members.Of(cardKey)?.Slug;

    /// <summary>A card's project and current piece, as the project files last read say; null when no project names it.</summary>
    public ProjectMember? MemberOf(string cardKey) => _projectReads.Members.Of(cardKey);

    /// <summary>A project as last read, by name, for the tools that write its file (1.1f B9); null when there is none.</summary>
    public ProjectFileReading? ProjectReadingOf(string name) =>
        _projectReads.Current.FirstOrDefault(reading => string.Equals(reading.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>What sessions said about their stages through now_at (1.1f E24); none until the app sets it.</summary>
    public StageReports StageReports { get; set; } = StageReports.None;

    /// <summary>
    /// Sets each card's project and slug from the members. A verifier no table names takes its subject's, its subject
    /// being the card it reports back to (1.1c plan, departure 1). Only a card whose place changed is touched.
    /// </summary>
    private Func<WorkstreamCardViewModel, ProjectMember?> ApplyMembership(ProjectMembers members)
    {
        var subjects = (Chains?.AllLinks ?? [])
            .Where(link => link.Kind == LinkKind.ReportsBack)
            .GroupBy(link => link.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().To, StringComparer.OrdinalIgnoreCase);

        ProjectMember? MemberOf(WorkstreamCardViewModel card) =>
            members.Of(card.Key)
            ?? (card.Workstream.Role == WorkstreamRole.Verifier && subjects.TryGetValue(card.Key, out var subject)
                ? members.Of(subject)
                : null);

        foreach (var card in Board.Cards)
        {
            var member = MemberOf(card);

            if (!string.Equals(card.Workstream.Project, member?.Project, StringComparison.Ordinal)
                || !string.Equals(card.Workstream.Slug, member?.Slug, StringComparison.Ordinal))
            {
                card.Workstream = card.Workstream with { Project = member?.Project, Slug = member?.Slug };
            }
        }

        return MemberOf;
    }

    /// <summary>
    /// The project cards on the board and the open project view (project-view AC 12 to 16). A readable project's cards
    /// leave the grid; an unreadable or unlisted project's come back to it (AC 13). With no readable project the board is
    /// exactly as before (AC 22).
    /// </summary>
    private void WireProjectBoard(IReadOnlyList<ProjectFileReading> readings, Func<WorkstreamCardViewModel, ProjectMember?> memberOf,
        bool anyMembers)
    {
        var readable = readings.Where(reading => reading.IsReadable).ToList();
        var names = readable.Select(reading => reading.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cards = Board.Cards.ToList();

        foreach (var card in cards.Where(card => card.Workstream.Project is not { } project || !names.Contains(project)))
        {
            card.ProjectSource = string.Empty;
        }

        foreach (var card in cards)
        {
            card.ProjectOfRole = card.Workstream.Role == WorkstreamRole.ScenarioWriter ? WriterProject(card.Workstream) : null;
        }

        Board.KeepOutOfGrid = ProjectGridRule.KeepOutOfGrid(memberOf, anyMembers);

        // Membership has just been read, so the holdout folders are looked at again by project (1.1e AC 6c).
        RefreshHoldouts([.. cards.Select(card => card.Workstream)]);
        RefreshStandingVerifiers();

        foreach (var card in cards)
        {
            SetVerifyTexts(card);
            card.BranchRulesSince = BranchRulesSince(card.Workstream);
        }

        var projectCards = new List<ProjectCardViewModel>();
        var queueCandidates = new List<QueueCandidate>();
        var queueSettings = _settings.Read();
        ProjectViewModel? openView = null;

        // Read once per wiring, not once per project.
        IReadOnlyList<SpecAcceptance> acceptances = readable.Count == 0 ? [] : _acceptances.Load();
        IReadOnlyList<ReportedAcceptance> reportedAcceptances = readable.Count == 0 ? [] : _reported.Load();
        _handIns = readable.Count == 0 ? new Dictionary<string, CardEvent>() : _cardEvents.LatestHandIns();
        _runStarts = readable.Count == 0
            ? new Dictionary<string, DateTimeOffset>()
            : _runs.Read().GroupBy(run => run.WorkstreamKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Max(run => run.StartedUtc), StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<PieceCard> pieceCards = readable.Count == 0 ? [] : [.. cards.Select(PieceCardOf)];
        var links = Chains?.AllLinks ?? [];
        var dimmed = readings.Where(reading =>
                !reading.IsReadable && reading.Folder is { } home && File.Exists(Path.Combine(home, ProjectCatalog.ProjectFileName)))
            .ToList();
        var colours = ProjectColoursFor([.. readable.Concat(dimmed).Select(reading => reading.Name)]);
        // The tabs carry each project's waits (board-small-changes item 2), so they are made, with the open view, once
        // every project's overview is read.
        var waitsByProject = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        (ProjectFileReading Reading, IReadOnlyList<PieceOfWork> Pieces, ProjectOverview Overview, string? Lead)? open = null;

        foreach (var reading in readable)
        {
            IReadOnlyList<PieceOfWork> pieces = PiecesOfWork.Build(reading, slug => acceptances.LastOrDefault(acceptance =>
                    string.Equals(acceptance.Project, reading.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(acceptance.Slug, slug, StringComparison.OrdinalIgnoreCase)),
                pieceCards, links, _projectReads.Members, StageReports);
            pieces = PiecesOfWork.WithReports(pieces, reading, reportedAcceptances);
            var working = pieces.Where(piece => piece.IsCurrentPiece && piece.Card is not null)
                .Select(piece => piece.CardKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<PieceCard> others = [.. cards
                .Where(card => string.Equals(card.Workstream.Project, reading.Name, StringComparison.OrdinalIgnoreCase) && !working.Contains(card.Key))
                .Select(PieceCardOf)];
            var checkPlan = CheckPlanOf(reading, pieces);
            var overview = ProjectOverview.For(reading.File!, pieces, links, Chains?.Proposals?.Pending.Select(row => row.Proposal) ?? [],
                acceptances.Count(acceptance => string.Equals(acceptance.Project, reading.Name, StringComparison.OrdinalIgnoreCase)),
                StartRequests.Pending, key => Board.Card(key)?.Title ?? key, WrongVerifiersOf(reading.Name), ClearUpNoticeOf(reading),
                checkPlan.Underway, others,
                WaitsOnTheChecks(reading, pieces, checkPlan, cards),
                OwnerAccount.Names);
            // A closed project's checks wait for it to be reopened, as its other starts do.
            if (!IsProjectClosed(reading.Name))
            {
                TakeCheckSteps(reading, pieces, checkPlan);
            }
            var file = reading.File!;

            foreach (var piece in pieces.Where(piece => piece.IsCurrentPiece && piece.Card is not null))
            {
                if (Board.Card(piece.CardKey) is { } card && reading.Artefacts.GetValueOrDefault(piece.Name) is { } artefacts)
                {
                    card.ProjectSource = artefacts.Source.Describe;
                }
            }
            var projectCard = Board.ProjectCards.FirstOrDefault(existing =>
                                  string.Equals(existing.Name, reading.Name, StringComparison.OrdinalIgnoreCase) && !existing.IsDimmed)
                              ?? new ProjectCardViewModel(reading.Name, file.Title);

            // A renamed project keeps its card, its selection and its focus, and shows the new title (1.1e AC 12a).
            projectCard.Title = file.Title;
            var name = reading.Name;

            projectCard.OpenCommand ??= new RelayCommand(_ => OpenProject(name));
            projectCard.Warning = _projectReads.Members.Clashes.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? $"A card this project names is also named by another project ({string.Join(", ", _projectReads.Members.Clashes.Where(other => !string.Equals(other, name, StringComparison.OrdinalIgnoreCase)))}); it shows in the first by name."
                : null;
            projectCard.OpenFileCommand ??= new RelayCommand(_ => OpenFile(Path.Combine(reading.Folder!, ProjectCatalog.ProjectFileName)));
            projectCard.AddObjectiveCommand ??= new RelayCommand(_ =>
                AddObjective(name, Readable(name)?.File?.CurrentPhase?.Number ?? Readable(name)?.File?.Phases.LastOrDefault()?.Number ?? 0));
            projectCard.PhaseText = file.CurrentPhase is { } phase ? $"Phase {phase.Number}: {phase.Name}" : "Every objective done";
            projectCard.ObjectivesText = $"{file.ObjectivesDone} of {file.Objectives.Count} objectives met";
            WireProjectCard(projectCard, reading, colours);
            var lead = ProjectLead.KeyOf(file, _settings.Read().ProjectLeads);
            var leadCard = lead is null ? null : Board.Card(lead);
            projectCard.FocusLeadToolTip = leadCard is null
                ? "This project has no lead card on the board"
                : FocusCardCommand.CanExecute(leadCard) ? $"Bring {leadCard.Title}'s terminal forward" : $"{leadCard.Title} has no session: start it first";
            projectCard.FocusLeadCommand = new RelayCommand(_ => FocusCardCommand.Execute(leadCard), _ => leadCard is not null && FocusCardCommand.CanExecute(leadCard));

            if (ProjectCardFace.Fill(projectCard, pieces, _settings.Read().ProjectRunStates.GetValueOrDefault(name),
                    _settings.Read().CardsStoppedByOwner, overview, lead, others))
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }

            WireStopping(projectCard, () => _launcher.CanLaunch);
            Runner.Wire(projectCard, () => _launcher.CanLaunch);

            // A closed or archived project is shown only under Archived, with Reopen: no tab, no waits, no queue.
            if (WireClosure(projectCard, name))
            {
                projectCards.Add(projectCard);

                continue;
            }

            // The badge counts what the summary sentence counts: one source (walkthrough row 6).
            IReadOnlyList<string> waiting = [.. overview.Waits.Select(wait => wait.Text)];

            if (!waiting.SequenceEqual(projectCard.Waiting))
            {
                projectCard.Waiting = waiting;
            }

            projectCards.Add(projectCard);
            waitsByProject[name] = overview.Waits.Count;

            if (string.Equals(name, _openProjectName, StringComparison.OrdinalIgnoreCase))
            {
                open = (reading, pieces, overview, lead);
            }

            GatherQueueCandidates(name, pieces, queueSettings, queueCandidates);
        }

        var tabs = ProjectTabViewModel.For(readable.Where(reading => !IsProjectClosed(reading.Name)), _openProjectName,
            name => new RelayCommand(_ => OpenProject(name)),
            name => ProjectColours.Of(colours, name), name => waitsByProject.GetValueOrDefault(name),
            name => new RelayCommand(_ => PutProjectAway(name, ProjectClosureKind.Closed)),
            name => new RelayCommand(_ => PutProjectAway(name, ProjectClosureKind.Archived)),
            name => projectCards.FirstOrDefault(card => string.Equals(card.Name, name, StringComparison.OrdinalIgnoreCase))?.StartCommand,
            name => projectCards.FirstOrDefault(card => string.Equals(card.Name, name, StringComparison.OrdinalIgnoreCase))?.StopCommand);
        SetProjectTabs(tabs);

        if (open is { } openOne)
        {
            openView = BuildView(openOne.Reading, openOne.Pieces, tabs, openOne.Overview, openOne.Lead);
        }

        _queueCandidates = queueCandidates;
        var openable = projectCards.Where(card => !card.IsPutAway).ToList();

        // A project whose file does not read still gets its card, dimmed and saying why (1.1c AC 5), with its mark and
        // colour but no Start or Stop (1.1d AC 44).
        // It says only that it needs updating, and Update, its double-click and Enter start a card that updates the file;
        // the reason is for that session, in the tooltip and Project properties (1.1e AC 18, 19). A folder the owner said
        // is not a project is not shown (AC 20); once it reads, it is a project again and leaves that list (review of PR
        // 4, finding 7).
        ReviewUpdaters(readings);
        var hidden = ProjectUpdate.StillHidden(_settings.Read().HiddenProjectFolders, names);

        if (hidden.Count != _settings.Read().HiddenProjectFolders.Count)
        {
            _settings.Write(_settings.Read() with { HiddenProjectFolders = hidden });

            foreach (var shown in Settings.HiddenProjectFolders.Where(name => !hidden.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList())
            {
                Settings.HiddenProjectFolders.Remove(shown);
            }
        }

        foreach (var reading in dimmed.Where(reading => !hidden.Contains(reading.Name, StringComparer.OrdinalIgnoreCase)))
        {
            var projectCard = Board.ProjectCards.FirstOrDefault(existing =>
                                  string.Equals(existing.Name, reading.Name, StringComparison.OrdinalIgnoreCase) && existing.IsDimmed)
                              ?? new ProjectCardViewModel(reading.Name, reading.Name) { IsDimmed = true };
            var updater = UpdaterOf(reading);
            var name = reading.Name;
            var problem = ProblemOf(reading);

            projectCard.OpenCommand ??= new RelayCommand(_ => UpdateProjectFile(name));
            projectCard.OpenFileCommand = null;
            projectCard.HideCommand ??= new RelayCommand(_ => HideProjectFolder(name));
            projectCard.Problem = problem;
            projectCard.PhaseText = ProjectUpdaterCard.Face(updater is { IsLive: true }, _settings.Read().ProjectUpdaterRuns.ContainsKey(name));
            projectCard.ObjectivesText = string.Empty;
            WireProjectCard(projectCard, reading, colours);
            ProjectCardFace.Dim(projectCard);

            if (projectCard.Waiting.Count > 0)
            {
                projectCard.Waiting = [];
            }

            projectCards.Add(projectCard);
        }

        Board.ShowProjects(projectCards);
        ArchiveDueCards(projectCards);
        ReleaseDueLocks(projectCards);

        var openChoices = openable
            .Select(card => new ProjectChoice(card.Name.Replace("_", "__", StringComparison.Ordinal), false, card.OpenCommand,
                [], $"Open {card.Title} in the main area") { ShowsTick = false })
            .ToList();

        if (!openChoices.SequenceEqual(OpenProjectChoices))
        {
            OpenProjectChoices = openChoices;
        }

        if (_openProjectName is { } shut && IsProjectClosed(shut))
        {
            CloseProject();
        }

        if (_openProjectName is not null && openView is null)
        {
            Warn($"{_openProjectName} can no longer be read, so its cards are back on the board.");
            CloseProject();

            return;
        }

        if (openView is not null && (_redrawProjectView || openView.Signature != ProjectView?.Signature))
        {
            ProjectView = openView;
        }

        _redrawProjectView = false;

        WireSpecChoices();
        WireProjectWindow(cards);
    }

    /// <summary>What every project card has, read or dimmed: its colour, its file, its properties and its selection.</summary>
    private void WireProjectCard(ProjectCardViewModel card, ProjectFileReading reading, IReadOnlyDictionary<string, string> colours)
    {
        card.ColourHex = ProjectColours.Of(colours, reading.Name) ?? ProjectCardViewModel.NoColour;
        card.ProjectFile = Path.Combine(reading.Folder ?? string.Empty, ProjectCatalog.ProjectFileName);
        card.PropertiesCommand ??= new RelayCommand(_ => ShowProjectProperties(card));
        card.SelectionChanged = SelectionMoved;
    }

    /// <summary>
    /// Which live session can bring the open project's window forward, or why none can (AC 21); asked each refresh, as
    /// sessions start and stop.
    /// </summary>
    private void WireProjectWindow(IReadOnlyList<WorkstreamCardViewModel> cards)
    {
        var target = _openProjectName is { } name
                     && Theseus.Core.Terminal.WindowGroupName.ForProject(_settings.Read().TerminalWindowName, name) is { } window
            ? Theseus.Core.Terminal.ProjectWindowTarget.For(name, window,
                [.. cards
                    .Where(card => string.Equals(card.Workstream.Project, name, StringComparison.OrdinalIgnoreCase))
                    .Select(card => (card.Key, card.Workstream.LastSessionId, card.IsLive))],
                RefreshRuns())
            : new Theseus.Core.Terminal.ProjectWindowTarget(null, "Open a project first");

        if (target != _projectWindow)
        {
            _projectWindow = target;
            Raise(nameof(ProjectWindowTooltip));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// The run log as this refresh read it: runs.jsonl grows without bound, so a refresh reads it once and shares it
    /// (PR 3 review, item 3). Read afresh only before the first refresh has.
    /// </summary>
    private IReadOnlyList<SessionRun> RefreshRuns() => _refreshRuns ??= _runs.Read();

    /// <summary>Brings the open project's terminal window forward, through one of its live sessions (AC 21).</summary>
    public RelayCommand BringProjectWindowForwardCommand { get; }

    /// <summary>What Bring the window forward does, or why it cannot, for its tooltip.</summary>
    public string ProjectWindowTooltip => _projectWindow.Reason;

    private void BringProjectWindowForward()
    {
        if (Sessions.Find(_projectWindow.SessionId) is not { } session)
        {
            Warn("The session that would bring the project's window forward is no longer running.");

            return;
        }

        SayFocus(TerminalFocus.FocusSession(session.Pid, session.Name), session.Name);
    }

    /// <summary>
    /// The open project's view, on the model's pieces of work (1.1d PR 1). The view it replaces passes on its selection,
    /// so a refresh that changes what is drawn keeps what the owner was looking at (AC 36).
    /// </summary>
    private ProjectViewModel BuildView(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces,
        IReadOnlyList<ProjectTabViewModel> tabs, ProjectOverview overview, string? lead) =>
        ProjectViewModel.Build(reading, pieces, Chains?.Status ?? new LinkStatus([], []), tabs, _settings,
            _projectActions ??= ProjectViewCommands.For(Board.Card, FocusCardCommand, StartCardCommand, StopCardCommand, OpenSpec,
                AcceptSpec, OpenFile, text => Notice = text, (card, project) => AddToProject(card, project), WhyNot, WayOn, Refuse) with
            {
                ShowRecords = ShowRecordsWindow is { } show ? (piece, stage) => show(new RecordsViewModel(piece, stage, OpenFile)) : null,
                OpenLinks = key => ShowCardLinksCommand.Execute(Board.Card(key)),
                CardActions = CardActionsOf,
                Jumped = Jumped,
                GoBack = GoBackCommand,
                GoForward = GoForwardCommand,
                ReviewProposal = ReviewProposal,
                TellCard = (key, text) => Tell(Board.Card(key), text),
                AnswerStartRequest = AnswerStartRequest,
                ClearVerifiers = ClearUpVerifiers,
                ResetBreaker = key => ResetBreakers([key]),
                ResetBreakerAndStart = ResetBreakerAndStart,
                OpenOptions = () => Settings.OpenCommand.Execute(null),
                StartVerifier = key =>
                {
                    if (Board.Card(key) is { } verifier)
                    {
                        StartVerifier(verifier);
                    }
                },
                UndoClearVerifiers = UndoClearUp,
                DismissClearUp = DismissClearUp,
                CloseProject = name => PutProjectAway(name, ProjectClosureKind.Closed),
                ArchiveProject = name => PutProjectAway(name, ProjectClosureKind.Archived),
                WhoStarts = WhoStartsCard,
                ReportOpened = _reportsOpened.HasOpened,
                OpenReport = (link, at, document) =>
                {
                    OpenFile(document);
                    _reportsOpened.Open(link, at);
                    Refresh();
                },
                ProposalsOf = key => [.. Chains?.Proposals?.Pending.Where(row =>
                    string.Equals(row.Proposal.RequesterKey, key, StringComparison.OrdinalIgnoreCase)) ?? []],
            },
            CloseProjectCommand,
            EditsOf(reading.Name),
            ProjectView,
            overview,
            lead);

    // F31's who-acts line for a card of a running project, from the same test the Start question uses (item 5).
    private ProjectRunStart? WhoStartsCard(string key)
    {
        if (Board.Card(key) is not { } card
            || card.Workstream.Role == WorkstreamRole.Verifier
            || ProjectCardGuard.RunningProjectOf(card, Board.ProjectCards) is not { } project)
        {
            return null;
        }

        var lead = project.LeadKey is { } leadKey && Board.Card(leadKey) is { IsLive: true } live ? live.Title : null;
        // A done card is not held: a project's sessions restart done cards (1.1e item 17).
        var heldByOwner = card.IsBlocked || card.IsPaused || card.StopReason?.Kind is WhyStoppedKind.PausedBy or WhyStoppedKind.StoppedByYou;
        IReadOnlyList<string> waitsOn = Chains?.Waiting([card.Workstream]).Count > 0
            ? [.. Chains.AllLinks.Where(link => link.IsActive && link.Kind == LinkKind.HandsOver
                                                && string.Equals(link.To, key, StringComparison.OrdinalIgnoreCase) && link.SatisfiedUtc is null)
                .Select(link => Board.Card(link.From)?.Title ?? link.From).Distinct()]
            : [];

        return new ProjectRunStart(Theseus.Core.Projects.WhoStarts.Line(project.Title, lead, heldByOwner, waitsOn));
    }

    /// <summary>What the project view knows of a board card (1.1d AC 1): its session, marks, role and whether Start would start it.</summary>
    private PieceCard PieceCardOf(WorkstreamCardViewModel card) =>
        new(card.Key, card.Title, card.ColorHex, card.Lifecycle, card.IsBlocked, card.Workstream.Role,
            StartSelection.IsEligible(card.Workstream, Sessions.Snapshot))
        {
            StopLine = card.StopReason?.Line,
            Question = card.Question,
            Prompt = card.Prompt,
            PausedBy = card.Workstream.PausedBy is { } by ? Board.Card(by)?.Title ?? by : null,
            BranchRulesSince = BranchRulesSince(card.Workstream),
            IsArchived = card.Workstream.IsArchived,
            HasRun = HasRun(card),
            StartFailure = _verifierStartFailures.GetValueOrDefault(card.Key),
            IsStarting = _launcher.InFlight.IsInFlight(card.Key),
            IsHeadless = Headless?.Holds(card.Key) == true,
            IsObsolete = IsObsolete(card),
            HandIn = HandInOf(card),
            LastRunStartedUtc = _runStarts.TryGetValue(card.Key, out var started) ? started : null,
        };

    // Read once per wiring (_handIns, _runStarts). Only work given by another card of the same project is a hand-in.
    private HandIn? HandInOf(WorkstreamCardViewModel card) =>
        _handIns.GetValueOrDefault(card.Key) is { By: { } by, Instruction: { } instruction } given
        && Board.Card(by) is { } from && card.Workstream.Project is { } project
        && string.Equals(from.Workstream.Project, project, StringComparison.OrdinalIgnoreCase)
            ? new HandIn(given.AtUtc, by, from.Title, instruction)
            : null;

    /// <summary>View spec and Accept spec on the menu bar, for the open project's waiting specs (R16).</summary>
    private void WireSpecChoices()
    {
        var specs = ProjectView?.WaitingSpecs ?? [];
        var open = specs.Select(spec => new ProjectChoice(spec.Slug, false, new RelayCommand(_ => OpenSpec(spec)), [],
            $"View the spec of {spec.Slug}, drawn, in its window; Accept spec… or Reject… there decides on the version on screen") { ShowsTick = false }).ToList();
        var accept = specs.Select(spec => new ProjectChoice(spec.Slug, false, new RelayCommand(_ => AcceptSpec(spec)), [],
            $"Accept the spec of {spec.Slug}, version {spec.ShortVersion}, after a confirmation") { ShowsTick = false }).ToList();

        if (!open.SequenceEqual(OpenSpecChoices))
        {
            OpenSpecChoices = open;
        }

        if (!accept.SequenceEqual(AcceptSpecChoices))
        {
            AcceptSpecChoices = accept;
        }
    }

    /// <summary>Back and Forward through the places the owner jumped to (1.1f I46).</summary>
    public ViewHistory History { get; } = new();

    /// <summary>◀ Back, Alt+Left and the mouse's back button: where the owner was before; from a project's first view, the board.</summary>
    /// <summary>
    /// Focus lead session, on the Workstream menu and Ctrl+L: brings the open project's lead terminal forward, as the
    /// header's Focus does (git-22, 2026-11-24: it was easy to miss). Disabled with no project open or no lead session.
    /// </summary>
    public System.Windows.Input.ICommand FocusLeadCommand => _focusLead ??= new RelayCommand(
        _ => ProjectView?.FocusLeadCommand?.Execute(null), _ => ProjectView?.FocusLeadCommand?.CanExecute(null) == true);

    /// <summary>The menu item's tooltip: what it would focus, or why it cannot.</summary>
    public string FocusLeadToolTip => ProjectView is { } view
        ? view.FocusLeadToolTip.Length == 0 ? "This project has no lead card" : view.FocusLeadToolTip
        : "Open a project to focus its lead session";

    private System.Windows.Input.ICommand? _focusLead;

    public System.Windows.Input.ICommand GoBackCommand => _goBack ??= new RelayCommand(_ => GoTo(History.Back()), _ => History.CanGoBack);

    /// <summary>Forward ▶, Alt+Right and the mouse's forward button.</summary>
    public System.Windows.Input.ICommand GoForwardCommand => _goForward ??= new RelayCommand(_ => GoTo(History.Forward()), _ => History.CanGoForward);

    private System.Windows.Input.ICommand? _goBack;
    private System.Windows.Input.ICommand? _goForward;
    private bool _goingTo;

    /// <summary>Records a jump to a place in the open project: a link followed, or a tab switched (I46).</summary>
    public void Jumped(string? selection)
    {
        if (!_goingTo && _openProjectName is { } project)
        {
            History.Jump(new ViewPlace(project, ProjectView?.Grouping.ToString(), selection));
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    // Shows a place from the history without recording it as a new jump.
    private void GoTo(ViewPlace? place)
    {
        if (place is null)
        {
            return;
        }

        _goingTo = true;

        try
        {
            if (place.IsBoard)
            {
                CloseProject();

                return;
            }

            if (!string.Equals(_openProjectName, place.Project, StringComparison.OrdinalIgnoreCase))
            {
                OpenProject(place.Project!);
            }

            ProjectView?.GoTo(place.Lens, place.Selection);
        }
        finally
        {
            _goingTo = false;
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Replaces the board with a project's view (AC 14). The drawer, side panels and status bar stay.</summary>
    public void OpenProject(string name)
    {
        LeaveAllProjects(forProject: true);
        _openProjectName = name;
        ProjectView = null;
        WireProjectChoices();

        if (ProjectView is null)
        {
            _openProjectName = null;
            Warn(IsProjectClosed(name) ? $"{name} is closed: reopen it from Archived on the board." : $"{name} cannot be read just now.");

            return;
        }

        _viewHost.Opened();
        _lastOpenedProject = name;

        if (Board.Search.IsOn)
        {
            ProjectView.RevealSearch();
        }

        SelectionMoved();
        Jumped(null);
        ContextChanged();
    }

    /// <summary>Back to the board, from ← Board, Alt+Left or View › Back to the board (1.1d AC 16).</summary>
    public void CloseProject()
    {
        if (!_goingTo && _openProjectName is not null)
        {
            History.Jump(ViewPlace.Board);
        }

        _openProjectName = null;
        LeaveAllProjects(forProject: false);
        ProjectView = null;
        _viewHost.Closed();
        SelectionMoved();
        WireSpecChoices();
        WireProjectChoices();
        ContextChanged();
    }

    /// <summary>The open project's view, or null while the board shows.</summary>
    public ProjectViewModel? ProjectView
    {
        get => _projectView;
        private set
        {
            if (Set(ref _projectView, value))
            {
                CarrySearch(value);
                Raise(nameof(FocusLeadToolTip));
                Raise(nameof(IsProjectOpen));
                Raise(nameof(IsBoardShown));
            }
        }
    }

    /// <summary>Whether a project view fills the main area.</summary>
    public bool IsProjectOpen => _openProjectName is not null && _projectView is not null;

    /// <summary>Whether the board fills the main area.</summary>
    public bool IsBoardShown => !IsProjectViewShown;

    /// <summary>Back to the board (AC 14).</summary>
    public RelayCommand CloseProjectCommand { get; }


    /// <summary>View › Open project: one item per readable project.</summary>
    public IReadOnlyList<ProjectChoice> OpenProjectChoices
    {
        get => _openProjectChoices;
        private set
        {
            if (Set(ref _openProjectChoices, value))
            {
                Raise(nameof(HasOpenProjectChoices));
            }
        }
    }

    /// <summary>Whether View shows Open project.</summary>
    public bool HasOpenProjectChoices => _openProjectChoices.Count > 0;

    /// <summary>Workstream › View spec, for the open project's waiting specs.</summary>
    public IReadOnlyList<ProjectChoice> OpenSpecChoices
    {
        get => _openSpecChoices;
        private set
        {
            if (Set(ref _openSpecChoices, value))
            {
                Raise(nameof(HasSpecChoices));
            }
        }
    }

    /// <summary>Workstream › Accept spec, likewise.</summary>
    public IReadOnlyList<ProjectChoice> AcceptSpecChoices
    {
        get => _acceptSpecChoices;
        private set => Set(ref _acceptSpecChoices, value);
    }

    /// <summary>Whether the Workstream menu shows View spec and Accept spec.</summary>
    public bool HasSpecChoices => _openSpecChoices.Count > 0;

    /// <summary>Asks the owner to accept a spec; true to accept. Set by the App to a message box.</summary>
    public Func<string, bool>? ConfirmAcceptSpec { get; set; }

    /// <summary>
    /// Accepts a spec on the board after a confirmation naming the slug and the commit, recording it in Theseus's own
    /// store and never in a file a session can write (AC 7, 15).
    /// </summary>
    public void AcceptSpec(SpecAtGate spec) => AcceptSpecConfirmed(spec);

    /// <summary>Accepts a spec after its confirmation; whether it was accepted.</summary>
    private bool AcceptSpecConfirmed(SpecAtGate spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // The question names the card it reaches and, when its session reported the owner's words, quotes them; the record
        // says who and how (1.1e AC 27 to 29).
        var card = CardOfPiece(spec.Project, spec.Slug);
        var report = _reported.For(spec.Project, spec.Slug) is { } said && string.Equals(said.ContentHash, spec.ContentHash, StringComparison.Ordinal)
            ? said
            : null;
        var how = report is null ? SpecAcceptance.OnTheBoard : SpecAcceptance.SaidIn(report.CardTitle);

        if (!_acceptances.AcceptIfConfirmed(spec, ConfirmAcceptSpec, DateTimeOffset.UtcNow, spec.QuestionFor(card?.Title, report, card?.IsLive ?? true),
                Environment.UserName, how))
        {
            return false;
        }

        _reported.Remove(spec.Project, spec.Slug);
        TheseusLog.Information("Accepted the spec of {Slug} in {Project}, version {Version}, {How}", spec.Slug, spec.Project, spec.ContentHash, how);
        var accepted = $"Accepted the {(spec.IsRule ? "rule" : "spec")} of {spec.Slug}, version {spec.ShortVersion}.";
        Notice = accepted;
        var told = TellAccepted(card, spec);
        Extend(string.Equals(Notice, accepted, StringComparison.Ordinal) ? $"{accepted} {told}" : $"{accepted} {told} {Notice}");
        WireProjectChoices();

        return true;
    }

    /// <summary>
    /// Tells the card of an accepted spec (1.1e AC 27): a running one with its next turn, as a stop request is told; a
    /// stopped one is started with the text. What was done, for the notice.
    /// </summary>
    private string TellAccepted(WorkstreamCardViewModel? card, SpecAtGate spec)
    {
        if (card is null)
        {
            return "No card works it, so no session was told.";
        }

        if (Tell(card, AcceptedTell.Text(spec)))
        {
            return $"{card.Title} is told with its next turn.";
        }

        StartMany([card.Workstream with { StartInstruction = AcceptedTell.Text(spec) }], trigger: "SpecAccepted");

        return $"Starting {card.Title}, told to carry on to the plan.";
    }

    /// <summary>
    /// Tells a card's session of something the owner did on the board (1.1e AC 27a): logged for events(), and given to a
    /// running session with its next turn, joined to anything else still waiting for it. Whether a session was running.
    /// It never starts a card.
    /// </summary>
    private bool Tell(WorkstreamCardViewModel? card, string text) =>
        card is not null && Tells.Tell(card.Key, card.Title, card.IsLive, card.Workstream.LastSessionId, text, DateTimeOffset.UtcNow);

    /// <summary>events() (1.1e AC 27a): what the owner did on the board that concerns the calling card, since it last asked.</summary>
    public string EventsFor(string callerKey) =>
        OwnerEvents.Describe(_ownerEvents.TakeNew(callerKey, DateTimeOffset.UtcNow), TimeZoneInfo.Local);

    /// <summary>A proposal the owner rejected on the board: its proposer is told, with his note (1.1e AC 27a).</summary>
    public void ProposalRejected(Theseus.Core.Proposals.WorkstreamProposal proposal, string? note)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        Tell(Board.Card(proposal.RequesterKey), OwnerEvents.Rejected(proposal.Request.Title, note));
    }

    /// <summary>The Branch cell of a card's row in its project file, or null.</summary>
    private string? RowBranchOf(Workstream workstream) =>
        workstream is { Project: { } project, Slug: { } slug }
            ? Readable(project)?.File?.Workstreams.LastOrDefault(row => string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase))?.Branch
            : null;

    /// <summary>The card that works a piece of work: its last row in the project file.</summary>
    private WorkstreamCardViewModel? CardOfPiece(string project, string slug) =>
        Readable(project)?.File?.Workstreams.LastOrDefault(row => string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase)) is { } row
            ? Board.Card(row.Workstream)
            : null;

    /// <summary>gates() (1.1e AC 28): the calling card's gate, for the version on disk now.</summary>
    public string GatesFor(string callerKey)
    {
        if (Board.Card(callerKey) is { Workstream.Role: not WorkstreamRole.None })
        {
            return SpecGates.NoGateForRole;
        }

        if (Board.Card(callerKey) is not { Workstream: { Project: { } project, Slug: { } slug } })
        {
            return "This workstream is not in a project's workstreams table, so it has no owner's gate: work as the owner asks in this tab.";
        }

        if (Readable(project) is not { } reading)
        {
            return $"{project}'s project.md does not read now, so its gates cannot be read. Ask the owner.";
        }

        var gate = SpecGates.State(SpecGates.SpecOf(reading, slug), _acceptances.Latest(project, slug), _reported.For(project, slug));

        return SpecGates.Describe(gate, project, slug, TimeZoneInfo.Local);
    }

    /// <summary>
    /// accept_spec (1.1e AC 28): keeps the owner's words for him to confirm on the board, or says why not. It accepts
    /// nothing.
    /// </summary>
    public string? ReportAcceptance(string callerKey, string slug, string version, string ownerWords)
    {
        if (Board.Card(callerKey) is not { } card)
        {
            return "This workstream is not on the board.";
        }

        var project = card.Workstream.Project;
        var reading = project is null ? null : Readable(project);
        var spec = reading is null ? null : SpecGates.SpecOf(reading, slug);

        if (SpecGates.WhoMayReport(project, card.Workstream.Slug, card.Workstream.Role, slug,
                spec is not null && _acceptances.WhyNotAccept(spec) is not null, spec?.ShortVersion) is { } cannot)
        {
            return cannot;
        }

        if (reading is null)
        {
            return $"{project}'s project.md does not read now, so its spec cannot be checked. Ask the owner.";
        }

        var (report, refusal) = SpecGates.Report(spec, version, card.Key, card.Title, ownerWords, DateTimeOffset.UtcNow);

        if (refusal is not null || report is null)
        {
            return refusal;
        }

        _reported.Report(report);
        TheseusLog.Information("{Card} reports the owner accepted the spec of {Slug}, version {Version}, in its tab", card.Key, slug, report.ContentHash);
        Notice = $"{card.Title}'s session reports you accepted the spec of {slug} in its tab: confirm it on the board, in {project}'s view.";
        WireProjectChoices();

        return null;
    }

    /// <summary>The session tools on its gate, for the MCP server.</summary>
    public Theseus.Core.Mcp.ISpecGates SpecGatesOnBoard => new SpecGatesOnBoard(GatesFor, ReportAcceptance);

    /// <summary>Shows a spec window. Set by the App; one window per piece of work, brought forward if open already.</summary>
    public Action<SpecWindowViewModel>? ShowSpec { get; set; }

    /// <summary>Shows a piece's Records window; set by the app, null in tests, where Open records opens the folder.</summary>
    public Action<RecordsViewModel>? ShowRecordsWindow { get; set; }

    /// <summary>The spec window's model for a spec as read (1.1e AC 24a).</summary>
    public SpecWindowViewModel SpecWindowFor(SpecAtGate spec) =>
        new(spec, shown => shown.ReadAgain(), WhyNotAcceptSpec, AcceptSpecConfirmed, shown =>
        {
            if (shown.FilePath is { } path)
            {
                OpenFile(path);
            }
        }, RejectSpec);

    /// <summary>
    /// Reject… in the spec window (board-small-changes item 4): nothing is recorded as decided, so the gate still waits, and the
    /// card is told so it writes a new version: a running one with its next turn, a stopped one through events() once it is
    /// started. Nothing is started (R3). What was done, for the window.
    /// </summary>
    private string RejectSpec(SpecAtGate spec, string? note)
    {
        var card = CardOfPiece(spec.Project, spec.Slug);
        var text = RejectedTell.Text(spec, note);
        TheseusLog.Information("Rejected the {Noun} of {Slug} in {Project}, version {Version}", spec.IsRule ? "rule" : "spec", spec.Slug,
            spec.Project, spec.ContentHash);
        string told;

        if (card is null)
        {
            told = "No card works it, so no session was told.";
        }
        else
        {
            // Never started: only Accept's question, which says so, may start a card (R3).
            told = Tell(card, text)
                ? $"{card.Title} is told with its next turn."
                : $"{card.Title} has no session; it is told when it next starts and asks what it missed.";
        }

        Notice = $"Rejected the {(spec.IsRule ? "rule" : "spec")} of {spec.Slug}, version {spec.ShortVersion}. {told}";

        return told;
    }

    /// <summary>
    /// Why Accept spec… cannot run for a version, or null (1.1e AC 24a): it is accepted already at that version, so its gate
    /// is passed until the spec changes.
    /// </summary>
    private string? WhyNotAcceptSpec(SpecAtGate spec) => _acceptances.WhyNotAccept(spec);

    /// <summary>
    /// View spec, wherever it appears (view, board, menus): the spec window, not the editor (1.1e AC 24a; Nigel, 2026-11-21).
    /// </summary>
    private void OpenSpec(SpecAtGate spec)
    {
        if (ShowSpec is { } show)
        {
            show(SpecWindowFor(spec));
        }
    }

    /// <summary>
    /// A report was confirmed on the board. A verifier's job ends there, so its card is stopped as a Stop press would
    /// stop it; any other finished card is ended by <see cref="EndFinishedCards"/> once it is idle.
    /// </summary>
    public void ReportConfirmed(Link link)
    {
        ArgumentNullException.ThrowIfNull(link);

        // Its recipient is told (1.1e AC 27a); in a project a stopped one is started with it (sessions-run-the-project 16a).
        var confirmed = OwnerEvents.Confirmed(link.Kind, Board.Card(link.From)?.Title ?? link.From, link.Output);

        // Only a report back starts its parent: accepting a hand-over tells the card it was for (review of PR 2, finding 3).
        if (!Tell(Board.Card(link.To), confirmed) && link.Kind == LinkKind.ReportsBack && Board.Card(link.To) is { } parent
            && ReportConfirmStart.StartsParent(parent.Workstream, parent.IsLive))
        {
            var before = parent.Workstream;

            if (before.IsPaused)
            {
                parent.Workstream = before with { IsPaused = false, PausedBy = null, PauseReason = null };
                Board.Persist(parent);
            }

            if (StartForSession([parent.Workstream with { StartInstruction = confirmed }], "ReportConfirmed") is { } notStarted)
            {
                parent.Workstream = parent.Workstream with { IsPaused = before.IsPaused, PausedBy = before.PausedBy, PauseReason = before.PauseReason };
                Board.Persist(parent);
                Warn(notStarted);
            }
            else
            {
                _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, parent.Key,
                    $"Started by your confirming {Board.Card(link.From)?.Title ?? link.From}'s report"));
            }
        }

        if (Board.Card(link.From) is not { IsLive: true } card
            || !VerifierEnding.StopsOnConfirm(link.Kind, card.Workstream.Role))
        {
            return;
        }

        TheseusLog.Information("Stopping {Verifier}: its report was confirmed, so its job is done", card.Key);
        BeginStop(CardSteps(card));
    }

    /// <summary>
    /// Asks the owner whether to allow a start request, given its title and question: true allows, false refuses, null
    /// leaves it waiting. Set by the App.
    /// </summary>
    public Func<string, string, bool?>? AskStartRequest { get; set; }

    /// <summary>
    /// The owner's answer to a session's request to start a card he paused (sessions-run-the-project items 13, 14): Allow
    /// clears his pause and starts it with the session's instruction; Refuse keeps the pause; either is told to the session.
    /// </summary>
    public void AnswerStartRequest(string requestId)
    {
        if (StartRequests.Find(requestId) is not { IsPending: true } request)
        {
            Warn("That request is no longer waiting.");

            return;
        }

        if (Board.Card(request.CardKey) is not { } card)
        {
            Warn($"{request.CardKey} is not on the board.");

            return;
        }

        var by = Board.Card(request.RequesterKey)?.Title ?? request.RequesterKey;
        var question = $"{by} wants to start {card.Title}, which you paused"
                       + (string.IsNullOrWhiteSpace(request.Reason) ? "." : $": {request.Reason.Trim()}")
                       + (string.IsNullOrWhiteSpace(request.Instruction) ? string.Empty : $"\n\nIts instruction: {request.Instruction.Trim()}");

        // The buttons say what they do, so the question does not explain Yes and No (baseline A3, G9).
        if (AskStartRequest?.Invoke($"Start {card.Title}, which you paused?", question) is not { } allow)
        {
            return;
        }

        if (!allow)
        {
            var note = AskText?.Invoke("Refuse the start", $"Why, for {by} (optional)", string.Empty);
            StartRequests.Decide(request.Id, StartRequestStatus.Refused, note);
            TellKey(request.RequesterKey, OwnerEvents.StartRefused(card.Title, note));
            Notice = $"Refused {by}'s start of {card.Title}; it stays paused.";

            return;
        }

        var paused = card.Workstream;
        card.Workstream = paused with { IsPaused = false, PausedBy = null, PauseReason = null };
        Board.Persist(card);

        // Allowed only once it launched; otherwise his pause is put back and the request keeps waiting (review of PR 2, finding 2).
        if (StartForSession([card.Workstream with { StartInstruction = request.Instruction }], "StartRequestAllowed") is { } notStarted)
        {
            card.Workstream = card.Workstream with { IsPaused = paused.IsPaused, PausedBy = paused.PausedBy, PauseReason = paused.PauseReason };
            Board.Persist(card);
            Warn($"{notStarted} Your pause stands and the request still waits.");

            return;
        }

        StartRequests.Decide(request.Id, StartRequestStatus.Allowed, null);
        _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, card.Key, $"Started by {by}, allowed by you")
        {
            By = string.IsNullOrWhiteSpace(request.Instruction) ? null : request.RequesterKey,
            Instruction = string.IsNullOrWhiteSpace(request.Instruction) ? null : request.Instruction.Trim(),
        });
        TellKey(request.RequesterKey, OwnerEvents.StartAllowed(card.Title));
    }

    /// <summary>Tells a card's session by its key, logged for events() even when the card is not on the board just now.</summary>
    private void TellKey(string key, string text)
    {
        if (Board.Card(key) is { } card)
        {
            Tell(card, text);
        }
        else
        {
            Tells.Tell(key, key, false, null, text, DateTimeOffset.UtcNow);
        }
    }

    /// <summary>
    /// Ends the requests that no longer apply (item 15): the owner resumed or started the card himself, it left the
    /// requester's project, it was archived, or either card is gone; the requester is told why.
    /// </summary>
    private void LapseStartRequests()
    {
        // While the archive is showing, the board holds only archived cards, so every other card would look gone (review
        // of PR 2, finding 1).
        if (Board.ShowArchived)
        {
            return;
        }

        foreach (var request in StartRequests.Pending)
        {
            var card = Board.Card(request.CardKey);
            var requester = Board.Card(request.RequesterKey);
            var why = StartRequestLapse.Why(card?.Workstream, card?.IsLive ?? false, requester?.Workstream);

            if (why is not null && StartRequests.Decide(request.Id, StartRequestStatus.Lapsed, why) is not null)
            {
                TellKey(request.RequesterKey, OwnerEvents.StartLapsed(card?.Title ?? request.CardKey, why));
            }
        }
    }

    /// <summary>
    /// Archives each ticked card that has no session, or restores it when the archive is showing.
    /// A card with a session running is left alone, as Archive on the card itself does.
    /// </summary>
    private void ArchiveSelected()
    {
        var chosen = Chosen();
        var quiet = chosen.Where(card => !card.IsLive).ToList();
        var restoring = Board.ShowArchived;

        foreach (var card in quiet)
        {
            Board.SetArchived(card, !restoring);
        }

        Refresh();

        var skipped = chosen.Count - quiet.Count;
        var done = restoring ? $"Restored {quiet.Count}." : $"Archived {quiet.Count}.";

        Notice = (skipped == 0 ? done : $"{done} Left {skipped} alone because a session is running.") + ProjectsSkipped()
                 + (restoring ? string.Empty : OfferWorktreeRemoval(quiet));
    }

    /// <summary>
    /// The pause button: a running session is stopped with a handoff and its card marked paused, so
    /// starting it again asks first; a card with no session has its paused mark turned on or off.
    /// </summary>
    /// <summary>The cards a card's menu acts on: the selection when this card is in it, else this card.</summary>
    private IReadOnlyList<WorkstreamCardViewModel> Targets(WorkstreamCardViewModel card) =>
        card.IsSelected && Chosen() is { Count: > 1 } chosen ? chosen : [card];

    /// <summary>Marks several cards at once, refreshing once rather than per card.</summary>
    private void SetFlagsOn(IReadOnlyList<WorkstreamCardViewModel> cards, Func<Workstream, Workstream> flag)
    {
        foreach (var card in cards)
        {
            card.Workstream = flag(card.Workstream);
            Board.Persist(card);
            Drawer.Reload(card);
        }

        Refresh();
    }

    /// <summary>
    /// The menu's Pause or Unpause, for every card it acts on: pausing stops each running session with
    /// a handoff and marks every card paused; unpausing clears the mark.
    /// </summary>
    private void PauseAll(IReadOnlyList<WorkstreamCardViewModel> cards, bool pause)
    {
        if (!pause)
        {
            SetFlagsOn(cards, workstream => workstream with { IsPaused = false });

            return;
        }

        // Pausing stops a live session, so a card of a running project is asked about as its ■ is (review of PR 2, finding 3).
        if (!RunCommands.Allowed([.. cards.Where(card => card.IsLive)], "Stop"))
        {
            return;
        }

        var steps = StepsFor([.. cards.Where(card => card.IsLive)]);

        SetFlagsOn(cards, workstream => workstream with { IsPaused = true });

        if (steps.Count > 0)
        {
            BeginStop(steps);
        }
    }

    private void Pause(WorkstreamCardViewModel card)
    {
        if (card.IsLive)
        {
            if (!RunCommands.Allowed([card], "Stop"))
            {
                return;
            }

            SetFlags(card, card.Workstream with { IsPaused = true });
            BeginStop(CardSteps(card));

            return;
        }

        SetFlags(card, card.Workstream with { IsPaused = !card.Workstream.IsPaused });
    }

    private void MarkSelected(Func<Workstream, Workstream> mark, string said)
    {
        var chosen = Chosen();

        foreach (var card in chosen)
        {
            card.Workstream = mark(card.Workstream);
            Board.Persist(card);
        }

        Refresh();
        Notice = (chosen.Count == 1 ? $"{chosen[0].Title} is {said}." : $"{chosen.Count} cards are {said}.") + ProjectsSkipped();
    }

    private void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            Fail($"{path} could not be opened: {exception.Message}");
        }
    }

    // Clearing reaches every card, shown or not; ticking reaches only what is shown (PR 2 review, item 1).
    private void ChooseAll(bool chosen)
    {
        if (chosen)
        {
            Board.ChooseShown(true);
        }
        else
        {
            Board.ClearSelection();
        }

        SelectionMoved();
    }

    /// <summary>Ticks what is not ticked and unticks what is, among the cards shown.</summary>
    private void Invert()
    {
        Board.InvertShown();
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
        Raise(nameof(PropertiesToolTip));
        Raise(nameof(VerifySelectedToolTip));
        Raise(nameof(VerifySelectedOffReason));
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

    private void BeginStop(IReadOnlyList<StopStep> steps, bool keepQueued = false)
    {
        // A stop asked for alongside another action, as Clear up verifiers… asks, leaves that action's queued steps alone.
        if (!keepQueued)
        {
            _stopQueue.Clear();
        }

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
        var underway = 0;
        var heldBack = 0;
        var stuck = new List<(string Title, StopState State)>();

        foreach (var step in steps)
        {
            // A headless run has no console to type into: its stop is written into the run itself (Nigel, 2026-11-24).
            if (Headless is { } runner && runner.Holds(step.WorkstreamKey))
            {
                TheseusLog.Information("Asked the headless run of {Workstream} to hand over and stop", step.WorkstreamKey);

                if (runner.AskToStop(step.WorkstreamKey))
                {
                    asked++;
                }
                else
                {
                    underway++;
                }

                continue;
            }

            var alreadyAsked = _stopRequests.Waiting(step.SessionId) is not null;

            var alreadyDelivered = StillHandingOver(step);

            switch (StopRouting.For(step.Lifecycle, installed, alreadyAsked, alreadyDelivered))
            {
                case StopRoute.AlreadyDelivered:
                    TheseusLog.Information(
                        "Did not ask {Workstream} again: it has already been handed its stop", step.WorkstreamKey);

                    // A stop held back when the board opened only closes once it is agreed to (fix pass 2, G1).
                    if (_unconfirmed.Contains(step.SessionId))
                    {
                        heldBack++;
                    }
                    else if (Board.Cards.FirstOrDefault(card => string.Equals(card.Key, step.WorkstreamKey, StringComparison.OrdinalIgnoreCase)) is { } stopping
                             && StopViewOf(stopping)?.State is { } state
                             && SecondPressNotice.IsStuck(state))
                    {
                        // Its last /exit did not land, so promising a close would be wrong: say what its card says
                        // (close-step AC 10).
                        stuck.Add((stopping.Title, state));
                    }
                    else
                    {
                        underway++;
                    }

                    break;

                case StopRoute.WaitForTurnEnd:
                    Ask(step);
                    asked++;
                    break;

                case StopRoute.Interrupt:
                case StopRoute.Nudge:
                    var cutIn = StopRouting.For(step.Lifecycle, installed, alreadyAsked) == StopRoute.Interrupt;

                    Ask(step);

                    // An idle session is woken through its console first, which takes no focus; typing into its tab is kept for
                    // when that cannot join the console, and for cutting a turn short.
                    var woken = cutIn ? ConsoleTypingOutcome.NotAttached : Wake(step, StopRouting.NudgeMarker);
                    var nudge = woken == ConsoleTypingOutcome.Sent ? NudgeOutcome.Sent
                        : NothingTyped(woken) ? TabNudge.Nudge(step.Pid, step.SessionName, StopRouting.NudgeMarker, cutIn)
                        : NudgeOutcome.Refused;

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

        // The refresh runs the close step, so a session already handed over and quiet is closed on this press.
        Refresh();
        Notice = SecondPressNotice.For(stuck, Describe(asked, waiting, unreached, blocked, interrupted, underway, heldBack),
            othersDescribed: stuck.Count < steps.Count, TimeZoneInfo.Local);
    }

    /// <summary>
    /// What was actually set in motion. A session that has been asked has not yet been told: the
    /// request waits until its next turn ends, so saying it has stopped would be ahead of the facts.
    /// </summary>
    private static string Describe(int asked, int waiting, int unreached, int blocked, int interrupted, int underway = 0,
        int heldBack = 0)
    {
        if (asked == 0 && unreached == 0 && blocked == 0 && interrupted == 0 && underway == 0 && heldBack == 0)
        {
            return "Nothing to stop.";
        }

        var said = new List<string>();

        if (heldBack > 0)
        {
            said.Add(heldBack == 1
                ? "That stop was held back when the board opened. Close them finishes it, with every other held-back stop."
                : $"{heldBack} of these stops were held back when the board opened. Close them finishes every held-back stop.");
        }

        if (underway == 1)
        {
            said.Add("That session already has its stop and is finishing it; it closes once it has handed over and gone quiet.");
        }
        else if (underway > 1)
        {
            said.Add($"{underway} sessions already have their stops and are finishing them; each closes once it has handed over and gone quiet.");
        }

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
        }, Sessions.Find(step.SessionId)?.StartedUtc);

        TheseusLog.Information("Asked {Workstream} ({SessionId}) to stop", step.WorkstreamKey, step.SessionId);

        // Only a stop delivered before the board opened is held back; one asked for now runs its course.
        if (_unconfirmed.Remove(step.SessionId))
        {
            StopsChanged();
        }
    }

    private bool StillHandingOver(StopStep step)
    {
        if (_stopRequests.Delivered(step.SessionId) is not { } handed
            || Sessions.Find(step.SessionId) is not { } running
            || SessionClosing.HandedBefore(handed, running.StartedUtc))
        {
            return false;
        }

        if (!SessionClosing.IsSpent(handed, running.Status, DateTimeOffset.UtcNow))
        {
            return true;
        }

        // Answered long ago and still open: left in place, it would turn away every later stop until it expired.
        if (_stopRequests.Retire(step.SessionId) && !_closed.IsFinished(handed))
        {
            StopTiming.ReportUnfinished(handed, delivered: true, StopRemoval.AskedAgain, DateTimeOffset.UtcNow);
        }

        return false;
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
            Fail("The stop request could not be put on the clipboard. The log has the reason.");

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

    /// <summary>
    /// The pull request and CI poller (pr-ci-status). Null until the app wires it, so a shell built in a test calls
    /// nothing on the network.
    /// </summary>
    public CiStatusViewModel? CiStatus
    {
        get => _ciStatus;
        set
        {
            if (_ciStatus is { } old)
            {
                old.Changed -= OnCiStatusChanged;
            }

            _ciStatus = value;

            if (value is not null)
            {
                value.Changed += OnCiStatusChanged;
            }

            Raise();
        }
    }

    private void OnCiStatusChanged(object? sender, EventArgs e) => ApplyCiStatus();

    /// <summary>Words every card's status again from the latest readings, without reading the repos' files again.</summary>
    private void ApplyCiStatus()
    {
        foreach (var card in Board.Cards)
        {
            card.SetStatus(CiStatus is { } ci && card.StatusRepos.Count > 0 ? ci.StatusFor(card.StatusRepos) : Theseus.Core.CiStatus.CardStatus.None);
        }
    }

    /// <summary>
    /// The repos a card's status covers: the ones its branch line shows, its declared repos or else its working
    /// directory's, each with its origin remote.
    /// </summary>
    private IReadOnlyList<Theseus.Core.CiStatus.CardStatusRepo> StatusRepos(string? workingDirectory, IReadOnlyList<CardRepo> repos)
    {
        if (repos.Count > 0)
        {
            return [.. repos.Select(repo => new Theseus.Core.CiStatus.CardStatusRepo(repo.Name, repo.Branch, _remotes.ReadOriginUrl(repo.Path)))];
        }

        if (GitRepository.RootOf(workingDirectory) is not { } root)
        {
            return [];
        }

        return [new Theseus.Core.CiStatus.CardStatusRepo(new DirectoryInfo(root).Name, GitBranchReader.Read(root), _remotes.ReadOriginUrl(root))];
    }

    /// <summary>Finds the pull requests each workstream mentions and where they live.</summary>
    public void SyncPullRequests()
    {
        var jiraBase = _settings.Read().JiraBaseUrl;

        foreach (var card in Board.Cards)
        {
            var recorded = WorkItems?.For(card.Key) ?? [];

            // The card's repos are found once per refresh and used for both its PR links and its branch line.
            var repos = CardRepos.Read(card.Workstream.AddDirectories);
            card.Repos = repos;
            card.PullRequests = _pullRequests.Resolve(card.Workstream, [.. repos.Select(repo => repo.Path)]);
            var line = BranchLines.For(card.Workstream.WorkingDirectory, repos);
            card.Branch = line.Text;
            card.BranchTooltip = line.Tooltip;
            card.BranchesDiffer = line.Differs;
            card.SetItems(
                CardItemRows.Jira(card.JiraKeys, recorded, jiraBase, key => WorkItems?.TitleOf(WorkItemKind.Jira, key)),
                CardItemRows.PullRequests(card.PullRequests, recorded, number => WorkItems?.TitleOf(WorkItemKind.PullRequest, number)));
            card.StatusRepos = card.IsArchived ? [] : StatusRepos(card.Workstream.WorkingDirectory, repos);
        }

        if (CiStatus is { } ci)
        {
            ci.SetRepositories([.. Board.Cards.SelectMany(card => card.StatusRepos)
                .Select(repo => Theseus.Core.CiStatus.GitHubRepoRef.FromRemote(repo.Origin))
                .OfType<Theseus.Core.CiStatus.GitHubRepoRef>()]);
        }

        ApplyCiStatus();

        // A search on branches or repos reads them only now they are known (card-search-and-tags item 2).
        if (Board.Search.IsOn)
        {
            Board.Regroup();
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
        _refreshRuns = runs;
        var preferred = _settings.Read().DefaultStartMode;
        var lastStage = Board.Stages.LastOrDefault();
        var reasons = _settings.Read();
        _branchGrants = reasons.ProjectBranchRules;
        RefreshHoldouts([.. Board.Cards.Select(card => card.Workstream)]);
        Questions.RemoveExcept(Sessions.Snapshot.Live.Select(session => session.SessionId), DateTimeOffset.UtcNow.AddMinutes(-10));
        Prompts.RemoveExcept(Sessions.Snapshot.Live
            .Where(session => string.Equals(session.Status, "waiting", StringComparison.OrdinalIgnoreCase))
            .Select(session => session.SessionId), DateTimeOffset.UtcNow);

        foreach (var card in Board.Cards)
        {
            var session = Sessions.Find(card.Workstream.LastSessionId);
            var live = session is not null;
            var question = live ? Questions.Read(card.Workstream.LastSessionId) : null;
            var state = CardLifecycle.For(card.Key, session?.Status, live, runs, now, hasOpenQuestion: question is not null);

            card.Question = question;
            card.Prompt = live ? Theseus.Core.Questions.PermissionPrompt.Showing(Prompts.Read(card.Workstream.LastSessionId), session?.Status, question) : null;

            if (live && card.Workstream.LastSessionId is { } registered)
            {
                _notYetRegistered.Remove(registered);
            }
            else if (card.Workstream.LastSessionId is { } launching && _notYetRegistered.TryGetValue(launching, out var launchedUtc)
                     && Theseus.Core.Questions.TrustPrompt.For(launching, isRegistered: false, _launcher.InFlight.IsInFlight(card.Key),
                         Settings.FolderTrust?.NeedsFor(SessionFolders(card.Workstream)).Select(need => need.Key).ToList() ?? [],
                         launchedUtc) is { } trust)
            {
                card.Prompt = trust;
                state = state with { Lifecycle = SessionLifecycle.NeedsAttention, Since = launchedUtc };
            }
            card.MotionAllowed = _motionAllowed;
            card.Lifecycle = state.Lifecycle;
            card.LifecycleSinceUtc = state.Since;
            card.LastExitedUtc = state.LastExitedUtc;
            card.LastStartedUtc = state.LastStartedUtc;
            card.IsInLastStage = lastStage is not null && string.Equals(card.Column, lastStage, StringComparison.OrdinalIgnoreCase);
            card.StopReason = CardStopReasons.For(card, reasons.ProjectRunStates, ProjectTitle, reasons.CardsStoppedByOwner,
                Chains?.AllLinks ?? [], key => Board.Card(key)?.Title ?? key);
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

            ApplyTranscript(card, live, preferred);

            // The other way to start, whichever the board prefers. Named explicitly in the log: the
            // caller's own name here would be this method's, which says nothing about the gesture.
            // The card's menu acts on the whole selection when the card is part of it, as a
            // right-click does in Explorer (Nigel, 2026-10-30); otherwise on this card alone.
            card.ResumeCommand ??= new RelayCommand(
                _ => StartChosen(card, StartMode.Resume, "ResumeCard"),
                _ => Targets(card).Any(target => CanStartCard(target) && target.CanResume));
            card.StartFreshCommand ??= new RelayCommand(
                _ => StartChosen(card, StartMode.Fresh, "StartFreshCard"),
                _ => Targets(card).Any(CanStartCard));

            // The card's menu carries the flags and Spawn follow-on, so a visit to the details is not
            // needed for them. A context menu cannot reach the window's commands, so these are the card's.
            card.ToggleBlockedCommand ??= new RelayCommand(_ =>
            {
                var blocked = !card.Workstream.IsBlocked;
                SetFlagsOn(Targets(card), workstream => workstream with { IsBlocked = blocked });
            });
            card.TogglePausedCommand ??= new RelayCommand(_ => SetFlags(card,
                card.Workstream with { IsPaused = !card.Workstream.IsPaused }));
            card.ToggleReopenTomorrowCommand ??= new RelayCommand(_ =>
            {
                var reopen = !card.Workstream.ReopenTomorrow;
                SetFlagsOn(Targets(card), workstream => workstream with { ReopenTomorrow = reopen });
            });
            card.SpawnFollowOnCommand ??= new RelayCommand(
                _ => SpawnFollowOnCommand.Execute(card),
                _ => Targets(card).Count == 1 && SpawnFollowOnCommand.CanExecute(card));
            card.PauseCommand ??= new RelayCommand(_ => Pause(card));
            card.CloseNowCommand ??= new RelayCommand(_ => CloseNow(card), _ => card.CanCloseNow);
            card.MenuPauseCommand ??= new RelayCommand(_ => PauseAll(Targets(card), pause: !card.IsPaused));
            card.PropertiesCommand ??= new RelayCommand(_ => ShowCard(card));
            card.VerifyWithHoldoutsCommand ??= new RelayCommand(_ => VerifyWithHoldouts(card), _ => CanVerify(card));
            card.AddToProjectCommand ??= new RelayCommand(_ => AddToProject(card),
                _ => _hasProjectsFolder && card.Workstream.Project is null && card.Workstream.Role == WorkstreamRole.None);
            card.RemoveFromProjectCommand ??= new RelayCommand(_ => RemoveFromProject(card),
                _ => card.Workstream.Project is not null && card.Workstream.Role == WorkstreamRole.None);
            card.ConvertToProjectCommand ??= new RelayCommand(_ => ConvertToProject(card),
                _ => _hasProjectsFolder && card.Workstream.Project is null && card.Workstream.Role == WorkstreamRole.None);
            card.RenameHandoffCommand ??= new RelayCommand(_ => RenameHandoff(card), _ => card.HandoffRenameTo.Length > 0);
            card.RunInTabCommand ??= new RelayCommand(_ => RunInTabCommand.Execute(card));
            card.RunHeadlessCommand ??= new RelayCommand(_ => RunHeadlessCommand.Execute(card));
            card.RunByDefaultCommand ??= new RelayCommand(_ => RunByDefaultCommand.Execute(card));
            card.OpenInTabCommand ??= new RelayCommand(_ => OpenInTabCommand.Execute(card), _ => OpenInTabCommand.CanExecute(card));
            card.ShowOutputCommand ??= new RelayCommand(_ => ShowOutputCommand.Execute(card), _ => ShowOutputCommand.CanExecute(card));
            card.ResetBreakerCommand ??= new RelayCommand(_ => ResetBreakerCommand.Execute(card), _ => card.IsBreakerTripped);
        }

        // After the lifecycles above, so a verifier that has just ended no longer stands (review of 72ec6d7).
        ArchiveClearedUp();
        RefreshStandingVerifiers();

        foreach (var card in Board.Cards)
        {
            SetVerifyTexts(card);
        }

        // Every card with a transcript, live or not: which model a stopped session was on is worth
        // knowing before restarting it.
        _transcripts.Refresh([.. Board.Cards.Select(card => (card.Workstream.LastSessionId, card.IsLive))]);

        Chains?.Update();

        var handoffs = Board.Cards.ToDictionary(card => card, card => HandoffPath.For(card.Workstream));

        var busy = BusyCards(runs);

        ResolveSharedHandoffs(handoffs, busy);
        SyncClashes(handoffs);
        SyncHandoffRenames(handoffs, busy);
        PublishSnapshot(handoffs);
        CloseFinishedSessions();

        // An updater's turn ending, or its session ending, changes its project card's face and may ask it to stop.
        var updaters = string.Join(",", Board.Cards.Where(card => card.Workstream.Role == ProjectUpdaterCard.Role)
            .Select(card => $"{card.Key}:{card.Lifecycle}"));

        if (ReviewUpdaters(_projectReads.Current) || !string.Equals(updaters, _updaterStates, StringComparison.Ordinal))
        {
            _updaterStates = updaters;
            WireProjectChoices();
        }
    }

    /// <summary>Shows on every card what the transcripts have told the board so far.</summary>
    private void ApplyTranscripts()
    {
        var preferred = _settings.Read().DefaultStartMode;

        foreach (var card in Board.Cards)
        {
            ApplyTranscript(card, card.IsLive, preferred);
        }
    }

    // The restart cost is only for a card that is offering Restart: there is nothing to say about a
    // session that is already running.
    private void ApplyTranscript(WorkstreamCardViewModel card, bool live, StartMode preferred)
    {
        var fact = _transcripts.Known(card.Workstream.LastSessionId);
        var measured = live ? default : fact.Measured;

        card.CanResume = measured.TotalBytes > 0;
        card.ResumeTooltip = Theseus.Core.Domain.RestartCost.Tooltip(measured.TailBytes, measured.TotalBytes);
        card.RestartCost = live
            ? string.Empty
            : StartModeChoice.Tooltip(preferred, card.CanResume, card.ResumeTooltip, card.Workstream.Role);
        card.Model = fact.Model ?? string.Empty;
    }

    // The cards shown, as they are, then every other card on disk (archived, or hidden by the archive view) as not
    // running, so the tools can answer about any card (review of 1.1e PR 3, finding 10).
    private Dictionary<WorkstreamCardViewModel, string>? _published;

    private void PublishSnapshot(Dictionary<WorkstreamCardViewModel, string> handoffs)
    {
        _published = handoffs;

        if (Snapshots is null)
        {
            return;
        }

        var shown = handoffs.Keys.Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Snapshots.Publish(new BoardSnapshot(
            [
                .. handoffs.Keys.Select(card => new BoardSnapshotCard(card.Workstream, handoffs[card],
                    card.Lifecycle, card.IsLive, card.IsInLastStage, card.LastStartedUtc, card.LastRunCrashed)
                {
                    StopLine = card.StopReason?.Line,
                    ProjectOfRole = card.Workstream.Role == WorkstreamRole.ScenarioWriter ? WriterProject(card.Workstream) : null,
                }),
                .. Board.AllWorkstreams().Where(workstream => !shown.Contains(workstream.Key))
                    .Select(workstream => new BoardSnapshotCard(workstream, HandoffPath.For(workstream),
                        SessionLifecycle.NotStarted, false, false, null, false)),
            ],
            DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Types /exit into any session that was asked to stop, has written its handover, and has gone
    /// quiet — the last step of stopping, which no session can take for itself.
    ///
    /// Nothing is killed. A killed process writes nothing, which is the whole reason stopping exists;
    /// this types what a person would type, and the session exits the ordinary way, which lets the
    /// launcher finish and the tab close on its own. It is typed into the session's own console, which
    /// takes no focus; typing into its tab, which refuses unless focus is in a terminal buffer, is kept
    /// for when that console cannot be joined.
    /// </summary>
    private void CloseFinishedSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var waiting = new List<DateTimeOffset>();

        EndFinishedCards(now, waiting);
        ResumeAfterTransportErrors(now, waiting);

        foreach (var card in Board.Cards)
        {
            if (CloseOne(card, now) is { Verdict: CloseVerdict.Wait, QuietEndsUtc: { } quietEnds })
            {
                waiting.Add(quietEnds);
            }
        }

        ArmCloseRecheck(CloseRecheck.Next(waiting));
        SyncStopStates();
    }

    /// <summary>The close step for one card: retires, closes, or says when it should be looked at again.</summary>
    private CloseDecision CloseOne(WorkstreamCardViewModel card, DateTimeOffset now)
    {
        if (card.Workstream.LastSessionId is not { } sessionId
            || Sessions.Find(sessionId) is not { } session
            || _unconfirmed.Contains(sessionId)
            || Headless?.Holds(card.Key) == true)
        {
            return new CloseDecision(CloseVerdict.Skip);
        }

        var delivered = _stopRequests.Delivered(sessionId);
        var handoffWritten = StopTiming.HandoffWrittenUtc(delivered?.HandoffPath);
        var decision = SessionClosing.Decide(delivered, session.StartedUtc, handoffWritten, session.Status,
            session.StatusSinceUtc, now, _closed.AlreadyTyped(delivered));

        if (decision.Verdict == CloseVerdict.Retire)
        {
            if (_stopRequests.Retire(sessionId) && !_closed.IsFinished(delivered))
            {
                StopTiming.ReportUnfinished(delivered!, delivered: true, StopRemoval.NewerSession, DateTimeOffset.UtcNow);
            }

            TheseusLog.Information(
                "Retired the stop request for {Workstream}: it was handed to an earlier run of the session, not the one running now",
                card.Key);

            return decision;
        }

        if (decision.Verdict != CloseVerdict.Close)
        {
            return decision;
        }

        _closed.Typed(delivered!);

        var typed = ConsoleTyping.TypeLine(session.Pid, SessionClosing.ExitCommand);
        var outcome = typed == ConsoleTypingOutcome.Sent ? NudgeOutcome.Sent
            : NothingTyped(typed) ? TabNudge.Nudge(session.Pid, session.Name, SessionClosing.ExitCommand)
            : NudgeOutcome.Refused;

        _closed.Attempted(delivered!, new ExitAttempt(ExitResults.From(outcome), DateTimeOffset.UtcNow));

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
            StopTiming.ReportFinished(delivered!, handoffWritten, DateTimeOffset.UtcNow);
            _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, card.Key, "Handed over, and /exit typed: its tab is closing"));

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

        return decision;
    }

    /// <summary>
    /// Runs the close step again at the given moment, or cancels the run when nothing is waiting. One timer, re-armed
    /// on every pass for the earliest waiting session, and running the close step only: a full refresh has taken
    /// seconds on this machine (close-step AC 1 to 3).
    /// </summary>
    private void ArmCloseRecheck(DateTimeOffset? when)
    {
        if (when is not { } due)
        {
            _closeRecheck?.Stop();

            return;
        }

        if (_closeRecheck is null)
        {
            _closeRecheck = new DispatcherTimer(DispatcherPriority.Background);
            _closeRecheck.Tick += (_, _) =>
            {
                _closeRecheck.Stop();

                // The sessions are re-read first: the snapshot from the last refresh can be about 20 s old, and a
                // session that left idle since would still look idle (close-step review S1).
                Sessions.Refresh();
                CloseFinishedSessions();
            };
        }

        var delay = due - DateTimeOffset.UtcNow;

        _closeRecheck.Stop();
        _closeRecheck.Interval = delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        _closeRecheck.Start();
    }

    /// <summary>Tells each card whether a stop is under way for it, how far it has got, and whether Close now is offered.</summary>
    private void SyncStopStates()
    {
        foreach (var card in Board.Cards)
        {
            var view = StopViewOf(card);

            card.IsStopping = view?.State is not null;
            card.StopTooltip = view is null ? string.Empty : StopStageText.For(view, TimeZoneInfo.Local);
            card.CanCloseNow = view?.CanCloseNow == true;
        }

        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    private StopView? StopViewOf(WorkstreamCardViewModel card)
    {
        if (card.Workstream.LastSessionId is not { } sessionId || Sessions.Find(sessionId) is not { } session)
        {
            return null;
        }

        var delivered = _stopRequests.Delivered(sessionId);

        return StopView.Of(_stopRequests.Waiting(sessionId), delivered, StopTiming.HandoffWrittenUtc(delivered?.HandoffPath),
            _closed, sessionId, session.StartedUtc, _unconfirmed.Contains(sessionId));
    }

    /// <summary>
    /// The owner pressed Close now: one more attempt at /exit, made only if the session is ready to close at this
    /// moment. Otherwise nothing is typed and nothing will retry it later on its own (close-step AC 9).
    /// </summary>
    private void CloseNow(WorkstreamCardViewModel card)
    {
        if (card.Workstream.LastSessionId is not { } sessionId
            || _stopRequests.Delivered(sessionId) is not { } delivered)
        {
            return;
        }

        // Read fresh, as on the timer: the press may come long after the last refresh (close-step review S1).
        Sessions.Refresh();

        if (_closed.TryCloseNow(delivered, () => CloseOne(card, DateTimeOffset.UtcNow)) is { Verdict: not CloseVerdict.Close } decision)
        {
            Warn(CloseNowText.NotClosed(card.Title, decision.Verdict));
        }

        SyncStopStates();
    }

    /// <summary>
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
              + (card.IsLive ? string.Empty : OfferWorktreeRemoval([card]))
            : $"Restored {title}.";
    }

    /// <summary>The cards whose sessions are running or being started, read once per refresh (AC 3).</summary>
    private HashSet<string> BusyCards(IReadOnlyList<SessionRun>? refreshRuns = null)
    {
        var runs = refreshRuns ?? _runs.Read();
        var now = DateTimeOffset.UtcNow;

        return Board.Cards
            .Where(card => HandoffBusy.Of(card.Key, card.IsLive, runs, now))
            .Select(card => card.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Makes the moves <see cref="SharedHandoffs.Plan"/> decides (handoff-names AC 1 to 4).</summary>
    private void ResolveSharedHandoffs(Dictionary<WorkstreamCardViewModel, string> handoffs, HashSet<string> busy)
    {
        var moves = SharedHandoffs.Plan(
            [.. handoffs.Select(entry => new HandoffCard(entry.Key.Key, entry.Key.Title, entry.Value,
                !string.IsNullOrWhiteSpace(entry.Key.Workstream.StatusDocumentPath), busy.Contains(entry.Key.Key)))],
            File.Exists, GitWorkingTree.ContainsPublishable, DateOnly.FromDateTime(DateTime.Now));

        var notices = new List<string>();

        foreach (var move in moves)
        {
            var card = handoffs.Keys.First(candidate => string.Equals(candidate.Key, move.Key, StringComparison.OrdinalIgnoreCase));

            try
            {
                if (move.PointerText is { } text)
                {
                    File.WriteAllText(move.OwnFile, text + Environment.NewLine, new System.Text.UTF8Encoding(false));
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Refresh runs on every registry change: one warning per file, not one per refresh.
                if (_handoffWriteFailures.Add(move.OwnFile))
                {
                    TheseusLog.Warning(exception, "Could not give {Workstream} a handover file of its own at {File}", card.Key, move.OwnFile);
                }

                continue;
            }

            card.Workstream = card.Workstream with { StatusDocumentPath = move.OwnFile };
            Board.Persist(card);
            handoffs[card] = move.OwnFile;

            TheseusLog.Information("{Notice}", move.Notice);
            notices.Add(move.Notice);
        }

        if (notices.Count > 0)
        {
            Notice = string.Join(" · ", notices);
        }
    }

    /// <summary>Tells each card whether its menu offers to rename its handover file to match its title (AC 5).</summary>
    private static void SyncHandoffRenames(Dictionary<WorkstreamCardViewModel, string> handoffs, HashSet<string> busy)
    {
        foreach (var (card, file) in handoffs)
        {
            card.HandoffRenameTo = RenameOffer(card, file, handoffs, busy) ?? string.Empty;
        }
    }

    private static string? RenameOffer(
        WorkstreamCardViewModel card, string file, Dictionary<WorkstreamCardViewModel, string> handoffs, HashSet<string> busy)
    {
        var target = HandoffRename.Target(file, card.Title);
        var shared = handoffs.Any(entry => entry.Key != card && string.Equals(entry.Value, file, StringComparison.OrdinalIgnoreCase));

        return HandoffRename.Offered(file, card.Title, busy.Contains(card.Key), shared, File.Exists(file), File.Exists(target));
    }

    /// <summary>
    /// Moves a card's handover file to the name its title gives, and makes that its handover file: one move, nothing
    /// copied or deleted. Checked again here, since the menu was built at the last refresh (AC 6).
    /// </summary>
    private void RenameHandoff(WorkstreamCardViewModel card)
    {
        var handoffs = Board.Cards.ToDictionary(other => other, other => HandoffPath.For(other.Workstream));
        var current = handoffs[card];
        var target = HandoffRename.Target(current, card.Title);

        if (RenameOffer(card, current, handoffs, BusyCards()) is null)
        {
            Warn($"{card.Title}'s handover file was not renamed: its session is running or starting, another card uses the file, "
                     + "the file is missing, or the new name is taken.");

            return;
        }

        try
        {
            HandoffRename.Apply(current, target);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not rename {Current} to {Target}", current, target);
            Fail($"{card.Title}'s handover file could not be renamed: {exception.Message}");

            return;
        }

        card.Workstream = card.Workstream with { StatusDocumentPath = target };
        Board.Persist(card);

        TheseusLog.Information("Renamed {Workstream}'s handover file from {Current} to {Target}", card.Key, current, target);
        Notice = $"Renamed {card.Title}'s handover file to {Path.GetFileName(target)}.";
        Refresh();
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
        if (inactiveOnly)
        {
            Board.FoldInactive();
        }
        else
        {
            Board.UnfoldAll();
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
            var handed = _stopRequests.Delivered(sessionId);

            if (_stopRequests.Retire(sessionId) && handed is not null)
            {
                StopTiming.ReportUnfinished(handed, delivered: true, StopRemoval.Declined, DateTimeOffset.UtcNow);
            }
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

            var starting = workstreams.Select(workstream => workstream.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var occupied = Board.Cards
                .Where(card => card.IsLive && !starting.Contains(card.Key))
                .Select(card => card.Title)
                .ToList();

            return AbandonedTabs.Among(workstreams, titles, Sessions.Snapshot, occupied);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            TheseusLog.Debug("The terminal's tabs could not be read before starting: {Reason}", exception.Message);

            return [];
        }
    }

    // With a project open, the main menu's selection commands act on the card of the box selected in the view (Nigel,
    // 2026-11-22: two cards he could not archive from the view).
    private IReadOnlyList<WorkstreamCardViewModel> Chosen() =>
        ProjectView?.Selection switch
        {
            PieceNodeViewModel node when Board.Card(node.Piece.CardKey) is { } card => [card],
            StageNodeViewModel stage when Board.Card(stage.Piece.Piece.CardKey) is { } card => [card],
            _ when ProjectView is not null => [],
            _ => Board.ChosenCards,
        };

    // A card's board actions on its box in the view (Nigel, 2026-11-22).
    private IReadOnlyList<PieceActionViewModel> CardActionsOf(PieceOfWork piece)
    {
        if (Board.Card(piece.CardKey) is not { } card)
        {
            return [];
        }

        var actions = new List<PieceActionViewModel>();
        var canArchive = ArchiveCardCommand.CanExecute(card);
        var verb = card.IsArchived ? "Restore" : "Archive";
        actions.Add(new PieceActionViewModel(verb, () => ArchiveCardCommand.Execute(card), canArchive,
            !canArchive ? $"{card.Title} has a session: stop it before archiving it"
            : card.IsArchived ? $"Restore {card.Title} to the board" : $"Archive {card.Title}: it leaves the board"));

        if (piece.IsListed && card.RemoveFromProjectCommand is { } remove)
        {
            actions.Add(new PieceActionViewModel("Remove from project", () => remove.Execute(null), remove.CanExecute(null),
                $"Take {card.Title}'s rows out of the project file"));
        }
        else if (!piece.IsListed && piece.ParentTitle is { } parent && piece.ParentCardKey is { } parentCard
                 && Chains?.AllLinks.FirstOrDefault(link => link.IsActive && link.Kind == LinkKind.HandsOver
                                                            && string.Equals(link.From, parentCard, StringComparison.OrdinalIgnoreCase)
                                                            && string.Equals(link.To, card.Key, StringComparison.OrdinalIgnoreCase)) is { } handOver)
        {
            actions.Add(new PieceActionViewModel($"Unlink from {parent}", () =>
            {
                if (Chains.RemoveHandOver(handOver.From, handOver.To) is { } refused)
                {
                    Warn(refused);
                }
            }, toolTip: $"Remove the hand-over that shows {card.Title} under {parent}"));
        }

        actions.Add(ShowTheCard.For(card, shown => ShowCard(shown, DrawerTab.Overview)));

        return actions;
    }

    private IReadOnlyList<Workstream> ChosenToStart() =>
        StartSelection.ForCards(
            [.. Chosen().Select(card => card.Workstream)], [.. Board.Stages], Sessions.Snapshot);

    private IReadOnlyList<Workstream> Workstreams() =>
        [.. Board.Cards.Select(card => card.Workstream)];

    private bool CanStartCard(object? parameter) =>
        parameter is WorkstreamCardViewModel card
        && _launcher.CanLaunch
        && StartSelection.IsEligible(card.Workstream, Sessions.Snapshot);

    /// <summary>A card's own ▶ (1.1e AC 10, 12): see <see cref="CardRunCommands.StartCard"/>.</summary>
    private void StartCard(object? parameter)
    {
        if (parameter is WorkstreamCardViewModel card)
        {
            RunCommands.StartCard(card);
        }
    }

    /// <summary>
    /// Starts what was asked for. Anything that already has a tab of its own name with nothing running in it starts too,
    /// and the board says so: a second tab beside an abandoned one surprised Nigel on 2026-09-07, and asking first made
    /// every Start two presses (Nigel, 2026-11-23).
    /// </summary>
    private void StartMany(IReadOnlyList<Workstream> workstreams,
        StartMode? startMode = null, bool evenIfWaiting = false, bool chosenOnTheCard = false, bool explain = true,
        [System.Runtime.CompilerServices.CallerMemberName] string trigger = "")
    {
        // Which gesture asked, recorded before anything can go wrong with it. There are four callers
        // — a card's own button, Start selected, Start without inputs, and creating a workstream — and
        // when one press produced two launches on 2026-09-08 nothing said whether one caller had run
        // twice or two callers had each run once. Those want different fixes.
        TheseusLog.Information("{Trigger} asked to start {Count} workstream(s) ({StartMode}): {Keys}",
            trigger, workstreams.Count, startMode?.ToString() ?? "board default", string.Join(", ", workstreams.Select(workstream => workstream.Key)));

        if (_launcher.Impediment is { } impediment)
        {
            Warn(impediment);

            return;
        }

        // A closed project starts nothing until the owner reopens it, whoever asks: a duty, a check, a session or a press.
        var settingsNow = _settings.Read();

        if (workstreams.Where(workstream => ProjectClosures.Holds(settingsNow, workstream)).ToList() is { Count: > 0 } held)
        {
            TheseusLog.Information("{Trigger}: not started, their project is closed: {Keys}", trigger, string.Join(", ", held.Select(workstream => workstream.Key)));
            Warn($"Not started: {string.Join(", ", held.Select(workstream => workstream.Title))}. Its project is closed: reopen it from Archived on the board first.");
            workstreams = [.. workstreams.Except(held)];

            if (workstreams.Count == 0)
            {
                return;
            }
        }

        if (FlaggedStart.Among(workstreams) is { Count: > 0 } flagged)
        {
            var others = workstreams.Count - flagged.Count;
            bool? answer = ConfirmFlaggedStart is { } confirm
                ? confirm(FlaggedStart.Question(flagged, others), others > 0)
                : false;

            TheseusLog.Information("Asked whether {Count} paused or blocked workstream(s) should start: {Answer}",
                flagged.Count, answer?.ToString() ?? "cancelled");

            if (answer is null)
            {
                return;
            }

            if (answer == true)
            {
                foreach (var workstream in flagged)
                {
                    if (Board.Card(workstream.Key) is { } card)
                    {
                        card.Workstream = FlaggedStart.Cleared(card.Workstream);
                        Board.Persist(card);
                    }
                }

                workstreams = [.. workstreams.Select(workstream =>
                    flagged.Contains(workstream) ? FlaggedStart.Cleared(workstream) : workstream)];
            }
            else
            {
                workstreams = [.. workstreams.Except(flagged)];

                if (workstreams.Count == 0)
                {
                    return;
                }
            }
        }

        // Asked about before anything else: a card still waiting on an input has nothing to read, and
        // is asked about whether one card was pressed or a whole selection.
        if (!evenIfWaiting && Chains?.Waiting(workstreams) is { Count: > 0 } waiting)
        {
            _heldForInputs = waiting;
            _heldForInputsMode = startMode;
            _heldForInputsChosenOnTheCard = chosenOnTheCard;
            InputsChanged();

            workstreams = [.. workstreams.Except(waiting)];

            if (workstreams.Count == 0)
            {
                return;
            }
        }

        // One press opens the tab (Nigel, 2026-11-23: "this seems onerous"): a tab already open under the same name
        // with nothing running in it is said on the board, not asked about first.
        var withOldTabs = Abandoned(workstreams);

        var started = new List<string>();
        var startedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var startedFolders = new List<string>();
        var refusals = new List<string>();
        var blocks = new List<(StartBlock Block, Workstream Asked)>();
        var worktreeNotes = new List<string>();

        foreach (var candidate in workstreams)
        {
            if (WithWorktrees(AimedWriter(Board.Pin(candidate)), refusals, worktreeNotes) is not { } workstream)
            {
                continue;
            }

            // What the owner's board actions told its last session, still waiting, is given with this start instead, so it
            // is neither lost nor told twice (review of PR 6, finding 3).
            if (Tells.TakeForStart(workstream.LastSessionId, DateTimeOffset.UtcNow) is { } waitingTell)
            {
                workstream = workstream with
                {
                    StartInstruction = string.IsNullOrWhiteSpace(workstream.StartInstruction)
                        ? waitingTell
                        : $"{workstream.StartInstruction}{Environment.NewLine}{Environment.NewLine}{waitingTell}",
                };
            }

            if (_launcher.Launch(workstream, _plans.PathFor(workstream.Key), startMode, chosenOnTheCard: chosenOnTheCard) is not { } sessionId)
            {
                if (_launcher.LastRefusal is { } refusal)
                {
                    refusals.Add(refusal);
                }

                if (_launcher.LastBlock is { } block)
                {
                    blocks.Add((block, workstream));
                }

                continue;
            }

            // Starting a workstream withdraws any request to stop it. A resumed session keeps its
            // identifier, so a request nobody collected outlives the session it was meant for and is
            // handed to the next one on its opening prompt - told to write a handoff and exit before
            // it has done anything at all.
            var undelivered = _stopRequests.Waiting(sessionId);

            if (_stopRequests.Cancel(sessionId))
            {
                TheseusLog.Information(
                    "Withdrew the stop request for {Workstream}, which is being started again", workstream.Key);

                if (undelivered is not null)
                {
                    StopTiming.ReportUnfinished(undelivered, delivered: false, StopRemoval.StartedAgain, DateTimeOffset.UtcNow);
                }
            }

            // A request already handed over is finished with too. On 2026-10-07 a workstream was asked
            // to stop, handed over, and was never closed; Nigel restarted it, and because the resumed
            // session kept the same identifier, the delivered request was still on disk. Two minutes
            // later the close step found it idle with a fresh handoff and typed /exit into the new one.
            var handed = _stopRequests.Delivered(sessionId);

            if (_stopRequests.Retire(sessionId))
            {
                TheseusLog.Information(
                    "Retired the delivered stop request for {Workstream}, which is being started again", workstream.Key);

                if (handed is not null && !_closed.IsFinished(handed))
                {
                    StopTiming.ReportUnfinished(handed, delivered: true, StopRemoval.StartedAgain, DateTimeOffset.UtcNow);
                }
            }

            Remember(workstream, sessionId);
            _notYetRegistered[sessionId] = DateTimeOffset.UtcNow;
            _verifierStartFailures.Remove(workstream.Key);
            RunCommands.Started(workstream.Key);
            started.Add(workstream.Title);
            startedKeys.Add(workstream.Key);
            startedFolders.AddRange(SessionFolders(workstream));
        }

        var untrusted = Settings.FolderTrust?.NeedsFor(startedFolders).Select(need => need.Key).ToList() ?? [];
        var oldTabs = withOldTabs.Where(workstream => startedKeys.Contains(workstream.Key)).Select(workstream => workstream.Title).ToList();
        Notice = string.Join(" ", new[]
        {
            StartNotice.For(started, refusals), worktreeNotes.Count == 0 ? null : string.Join(" ", worktreeNotes),
            StartNotice.OldTabLine(oldTabs), StartNotice.TrustLine(untrusted),
        }.OfType<string>());

        Refresh();

        if (explain)
        {
            ExplainBlocked(blocks, startMode, evenIfWaiting, chosenOnTheCard);
        }
    }

    /// <summary>
    /// Asks whether to reset a breaker and start, with the reason, and starts what it resets (Nigel, 2026-11-25). Set by the
    /// app; without it a refused start is explained with <see cref="ExplainRefusal"/>.
    /// </summary>
    public Func<string, string, string, bool>? AskRefusedStart { get; set; }

    // A start a breaker or limit refused says so at once where it was pressed, with what clears it: on 2026-11-25 it was
    // only a log Warning and Nigel saw "Start again did not do anything".
    private void ExplainBlocked(IReadOnlyList<(StartBlock Block, Workstream Asked)> blocks, StartMode? startMode, bool evenIfWaiting,
        bool chosenOnTheCard)
    {
        if (RefusedStart.For([.. blocks.Select(blocked => blocked.Block)]) is not { } refused)
        {
            return;
        }

        if (refused.Action is not { } action || AskRefusedStart is not { } ask)
        {
            Refuse(refused.Title, refused.Text);

            return;
        }

        if (ask(refused.Title, refused.Text, action))
        {
            ResetBreakers(refused.BreakerKeys);

            // The card as reset, with what the refused start carried: an accepted spec's tell, or a tell already taken for it.
            var again = blocks
                .Where(blocked => refused.BreakerKeys.Contains(blocked.Block.Key, StringComparer.OrdinalIgnoreCase))
                .Select(blocked => Board.Card(blocked.Block.Key) is { } card
                    ? card.Workstream with { StartInstruction = blocked.Asked.StartInstruction }
                    : null)
                .OfType<Workstream>()
                .ToList();
            StartMany(again, startMode, evenIfWaiting, chosenOnTheCard, trigger: "ResetBreakerAndStart");
        }
    }

    // The folders a session starts in or adds, which Claude Code asks the owner to trust.
    private static IEnumerable<string> SessionFolders(Workstream workstream) =>
        new[] { workstream.WorkingDirectory }.Concat(workstream.AddDirectories);

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
        IReadOnlyList<string> taken = [.. _plans.Keys(), .. Chains?.TakenKeys ?? []];
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
        parameter is WorkstreamCardViewModel card
        && (Sessions.Find(card.Workstream.LastSessionId) is not null
            || (card.Workstream.LastSessionId is { } launching && _notYetRegistered.ContainsKey(launching) && _launcher.InFlight.IsInFlight(card.Key)));

    private void FocusCard(object? parameter)
    {
        if (parameter is not WorkstreamCardViewModel card)
        {
            return;
        }

        // A tab at Claude Code's folder trust question has no session in the registry yet; Theseus titled the tab itself.
        if (Sessions.Find(card.Workstream.LastSessionId) is not { } session)
        {
            if (card.Workstream.LastSessionId is { } launching && _notYetRegistered.ContainsKey(launching)
                && _launcher.InFlight.IsInFlight(card.Key))
            {
                SayFocus(TerminalFocus.FocusSession(0, card.Title), card.Title);
            }

            return;
        }

        SayFocus(TerminalFocus.FocusSession(session.Pid, session.Name), card.Title);
    }

    /// <summary>
    /// Says what the focus actually achieved. Landing on the window but not the tab is the normal
    /// outcome for a session Theseus did not launch, and saying so beats leaving the operator to
    /// wonder why the wrong tab is showing.
    /// </summary>
    private void SayFocus(FocusOutcome outcome, string title) =>
        SetNotice(outcome == FocusOutcome.TabSelected ? NoticeSeverity.Information : NoticeSeverity.Warning, Describe(outcome, title));

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

        Warn(string.IsNullOrWhiteSpace(_settings.Read().JiraBaseUrl)
            ? $"Set your Jira address in Settings to open {key}."
            : $"{key} does not look like an issue in the configured Jira.");
    }
}
