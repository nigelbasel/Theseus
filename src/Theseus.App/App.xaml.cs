using System.IO;
using System.Windows;
using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Colour;
using Theseus.Core.Domain;
using Theseus.Core.Diagnostics;
using Theseus.Core.Launching;
using Theseus.Core.Links;
using Theseus.Core.Locks;
using Theseus.Core.Messaging;
using Theseus.Core.Persistence;
using Theseus.Core.Sessions;
using Theseus.Core.Settings;
using Theseus.Core.Usage;
using Theseus.Core.Terminal;
using Theseus.Core.Updates;

namespace Theseus.App;

/// <summary>Composition root. Everything the window needs is built here and nowhere else.</summary>
public partial class App : Application
{
    private LaunchCoordinator? _launcher;
    private SessionRegistryWatcher? _watcher;
    private UsageViewModel? _usage;
    private ResourcesViewModel? _resources;
    private AttentionSampler? _attention;
    private GitHubReleaseFeed? _releases;
    private System.Windows.Threading.DispatcherTimer? _updateTimer;
    private McpHttpServer? _mcp;
    private Timer? _lockSweep;
    private LocksViewModel? _locks;

    /// <summary>Where Theseus keeps the state the plan files have no room for.</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus");

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(DataDirectory);
        TheseusLog.Start(Path.Combine(DataDirectory, "logs"));

        if (FocusRequest.SessionIdIn(e.Args) is { } wanted)
        {
            Focus(wanted);
            Shutdown();
            return;
        }

        if (!SingleInstance.Claim())
        {
            Shutdown();

            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            TheseusLog.Error(args.Exception, "An unhandled exception reached the dispatcher");
            args.Handled = false;
        };

        var settings = new SettingsStore(Path.Combine(DataDirectory, "settings.json"));
        var current = settings.Read();

        Directory.CreateDirectory(current.PlanDirectory);

        var plans = new PlanStore(current.PlanDirectory);
        var boardStore = new BoardStore(Path.Combine(DataDirectory, "board.json"));

        // Before the board is built, not after: it reads the settings this may rewrite.
        BoardMigration.Run(settings, boardStore, plans);
        BoardMigration.Tidy(settings, boardStore);

        var board = new BoardViewModel(plans, boardStore, settings);
        var sessions = new SessionsViewModel(
            new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe()),
            new ConsoleTitleReader());

        var runs = new RunLog(Path.Combine(DataDirectory, "runs.jsonl"));
        var sink = new BoardLaunchEventSink(board, Dispatcher, runs);

        _launcher = new LaunchCoordinator(
            sink,
            new CachedTerminalResolver(new TerminalResolver(current.TerminalExecutablePath), new SystemClock()),
            new ClaudeLocator(current.ClaudeExecutablePath),
            new SessionIdFactory(),
            runs,
            new SystemClock(),
            ShimProtocol.DefaultPipeName,
            LauncherPath,
            settings,
            sink.Expect);

        var shell = new ShellViewModel(board, sessions, settings, _launcher, plans, runs,
            new SessionTodoReader(SessionTodoReader.DefaultDirectory), new TabColourAllocator(),
            new RunEndings(DateTimeOffset.UtcNow));

        // Sessions report on links from the MCP server's threads, so a change made off the UI thread
        // redraws the board. The board's own changes already refresh it.
        var links = new LinkBook(new LinkStateStore(Path.Combine(DataDirectory, "links.json")), new SystemClock())
        {
            CurrentPieceOf = shell.CurrentPieceOf,
        };
        var snapshots = new BoardSnapshotSource();
        var chains = new ChainsViewModel(
            links,
            () => [.. shell.Board.Cards],
            () => settings.Read().BriefFolderName,
            shell.Refresh,
            key => shell.ShowCardLinksCommand.Execute(shell.Board.Card(key)));

        shell.Chains = chains;
        shell.Snapshots = snapshots;
        chains.ReportArrived = shell.Warn;
        chains.ReportConfirmed = shell.ReportConfirmed;

        var workItems = new WorkItemBook(Path.Combine(DataDirectory, "work-items.json"));
        shell.WorkItems = workItems;
        workItems.Changed += (_, _) => Dispatcher.BeginInvoke(shell.SyncPullRequests);
        var proposals = new Theseus.Core.Proposals.ProposalBook(
            new Theseus.Core.Proposals.ProposalStateStore(Path.Combine(DataDirectory, "proposals.json")), new SystemClock());
        var proposed = new ProposalsViewModel(proposals, () => [.. shell.Board.Cards], shell.ReviewProposal)
        {
            Arrived = shell.Warn,
            Rejected = shell.ProposalRejected,
            ShowBrief = brief => new RecordsWindow(new RecordsViewModel($"Brief: {Path.GetFileName(Path.GetDirectoryName(brief))}",
                new Theseus.Core.Projects.RecordEntry("Brief", "What the proposing session asks the new workstream to do",
                    Theseus.Core.Projects.StageNodeState.Done, brief) { StateText = "written" },
                path => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true })))
            {
                Owner = MainWindow,
            }.Show(),
        };

        chains.Proposals = proposed;
        proposals.Changed += (_, _) => Dispatcher.BeginInvoke(proposed.Update);
        sink.StartLost = shell.Warn;
        links.Changed += (_, _) =>
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(shell.Refresh);
            }
        };
        _launcher.RelatedFor = chains.RelatedParagraph;
        _launcher.RelatedSince = chains.RelatedSince;

        if (ChildSessionCheck.Warning() is { } warning)
        {
            TheseusLog.Warning("{Warning}", warning);
            shell.Warn(warning);
        }

        if (shell.Settings.Guard is { } guard)
        {
            guard.ConfirmChange = change => MessageBox.Show(MainWindow, change, "Add the holdout guard's hook entry?",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        if (shell.Settings.FolderTrust is { } trust)
        {
            trust.ConfirmChange = change => MessageBox.Show(MainWindow, change, "Trust these folders in Claude Code?",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        }

        // Said once for each newer guard an update brings, until it is installed from Options.
        if (HoldoutGuardNotice(settings) is { } guardNotice)
        {
            TheseusLog.Warning("{Warning}", guardNotice);
            shell.Warn(guardNotice);
        }
        shell.Refresh();
        proposed.Update();
        shell.ApplyTheme();

        _watcher = new SessionRegistryWatcher(
            SessionRegistryReader.DefaultDirectory,
            new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe()));

        _watcher.Changed += (_, _) => Dispatcher.BeginInvoke(shell.Refresh);

        _usage = new UsageViewModel(
            new OAuthUsageClient(new CredentialFileSource(), new SystemClock(), ClaudeCodeVersion),
            new UsagePollSchedule(new SystemClock()),
            new SystemClock());

        shell.Usage = _usage;

        _resources = new ResourcesViewModel(
            () => [.. shell.Board.Cards],
            sessionId => shell.Sessions.Find(sessionId)?.Pid,
            current.CoreClamp);
        shell.Resources = _resources;

        _attention = new AttentionSampler(
            () => [.. shell.Board.Cards.Select(card => card.Workstream)],
            new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe()),
            new Theseus.Core.Attention.AttentionLog(Path.Combine(DataDirectory, "attention.jsonl")));
        _attention.Start();

        // Sessions say what they are working on here; the time comes from the sampler above. The
        // session a request belongs to is found in the run log, off the UI thread, so a busy board
        // never keeps a session waiting on a tool call.
        var attention = new Theseus.Core.Attention.AttentionLog(Path.Combine(DataDirectory, "attention.jsonl"));

        var clock = new SystemClock();
        var testContainers = new DockerTestContainers();
        var locks = new LockBook(
            new LockStateStore(Path.Combine(DataDirectory, "locks.json")),
            new LockHistory(Path.Combine(DataDirectory, "locks-history.jsonl")),
            new RegistryLockLiveness(new ProcessProbe(), new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe()), clock),
            clock,
            new WindowsProcessTree(new ProcessProbe()),
            OrphanCleaner.ForThisMachine(new ProcessProbe(), testContainers));

        _lockSweep = new Timer(_ => locks.Sweep(), null, TimeSpan.Zero, TimeSpan.FromSeconds(15));
        _locks = new LocksViewModel(locks, () => DateTimeOffset.UtcNow, ticket => LockColours.For(ticket, shell.Board.Cards), testContainers);
        shell.Locks = _locks;

        // Locks and Chains share the right-hand side and never sit side by side: with the sessions
        // panel open as well, two would leave the board one column wide.
        _locks.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LocksViewModel.IsOpen) && _locks.IsOpen)
            {
                chains.IsOpen = false;
            }
        };
        chains.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ChainsViewModel.IsOpen) && chains.IsOpen)
            {
                _locks.IsOpen = false;
            }
        };

        var stageReports = new Theseus.Core.Projects.StageReportBook(Path.Combine(DataDirectory, "stage-skips.json"));
        shell.StageReports = stageReports.Reports;
        stageReports.Changed += (_, _) => Dispatcher.BeginInvoke(shell.Refresh);

        if (_launcher is not null)
        {
            _launcher.LaunchedCard = stageReports.Forget;
        }

        _mcp = new McpHttpServer(
            new Theseus.Core.Mcp.McpHandler(
                new Theseus.Core.Attention.WorkLog(Path.Combine(DataDirectory, "work.jsonl")),
                attention.Read,
                sessionId => runs.Read()
                    .Where(run => string.Equals(run.ClaudeSessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(run => run.StartedUtc)
                    .Select(run => new Theseus.Core.Mcp.McpSession(run.ClaudeSessionId, run.WorkstreamKey, run.TabTitle))
                    .FirstOrDefault(),
                () => DateTimeOffset.UtcNow,
                locks: locks,
                links: links,
                board: () => snapshots.Current,
                paths: new SessionPathRule(current.PlanDirectory, DataDirectory),
                proposals: proposals,
                briefFolderName: () => settings.Read().BriefFolderName,
                approve: (proposal, startNow) => Dispatcher.Invoke(() => shell.ApproveFromSession(proposal, startNow)),
                workItems: workItems,
                control: new DispatchedWorkstreamControl(shell.WorkstreamControl, Dispatcher),
                gates: new DispatchedSpecGates(shell.SpecGatesOnBoard, Dispatcher),
                // Read off the UI thread: the log has its own lock, and a board busy with a dialog must not lose what it marks
                // read (review of PR 6, finding 2).
                events: shell.EventsFor,
                projectTools: new Theseus.Core.Mcp.ProjectTools(shell.MemberOf, shell.ProjectReadingOf, stageReports,
                    () => DateTimeOffset.UtcNow, write: (project, edit) => Dispatcher.Invoke(() => shell.WriteProjectFile(project, edit))),
                create: (proposal, startNow) => Dispatcher.Invoke(() => shell.CreateForSession(proposal, startNow)),
                askOwner: new Theseus.Core.Mcp.AskOwnerTool(shell.Questions, () => DateTimeOffset.UtcNow,
                    () => Dispatcher.BeginInvoke(shell.Refresh)),
                startRequests: shell.StartRequests),
            Theseus.Core.Mcp.TheseusMcp.DefaultPort,
            new LockHttpApi(locks, () => DateTimeOffset.UtcNow));

        if (_mcp.Start() && _launcher is not null)
        {
            var port = _mcp.Port;
            _launcher.McpConfigFor = sessionId => Theseus.Core.Mcp.TheseusMcp.ConfigJson(port, sessionId);

            // Sessions learn from a file, not an event, that Theseus has stopped or restarted: once it
            // has gone it can tell them nothing (Nigel, 2026-10-30).
            var hook = Path.Combine(AppContext.BaseDirectory, "Theseus.Hook.exe");
            _launcher.SessionSettings = File.Exists(hook) ? Theseus.Core.Status.TheseusHookSettings.Json(hook) : null;
            _launcher.Launched = sessionId => new Theseus.Core.Status.StatusSeenStore().Write(sessionId,
                Theseus.Core.Status.TheseusStatusNotice.Marker(Theseus.Core.Status.TheseusStatusFile.Read(),
                    Theseus.Core.Status.TheseusStatusFile.IsRunning));
            Theseus.Core.Status.TheseusStatusFile.Write(new Theseus.Core.Status.TheseusStatus(
                Environment.ProcessId, DateTimeOffset.UtcNow, null, AppVersion.Current));
        }

        SessionJumpList.Show(this, sessions.Snapshot.Live);
        sessions.Refreshed += (_, _) => SessionJumpList.Show(this, sessions.Snapshot.Live);

        StartUpdateChecks(shell, settings);

        MainWindow = new MainWindow { DataContext = shell };
        shell.ConfirmFlaggedStart = (question, hasOthers) =>
        {
            var answer = MessageBox.Show(MainWindow, question, "Start paused or blocked work?",
                hasOthers ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo, MessageBoxImage.Question,
                MessageBoxResult.No);

            return answer switch
            {
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                _ => null,
            };
        };
        shell.ConfirmProjectCard = (question, action) =>
            new QuestionWindow($"{action} a card of a running project?", question, action) { Owner = MainWindow }.ShowDialog() == true;
        // Owned by the active window, so one opened from Options sits above it rather than behind it.
        shell.AskProjectProperties = properties =>
            new ProjectPropertiesWindow(properties) { Owner = Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? MainWindow }
                .ShowDialog() == true;
        // Owned by the window it was asked from, so a spec window is blocked behind its question (review of PR 5, 13).
        shell.ConfirmAcceptSpec = question =>
            MessageBox.Show(Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? MainWindow, question, "Accept spec?",
                MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;
        shell.ExplainRefusal = text =>
            MessageBox.Show(Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? MainWindow, text, "That cannot be done yet",
                MessageBoxButton.OK, MessageBoxImage.Information);
        shell.AskStartRequest = question =>
            MessageBox.Show(Windows.OfType<Window>().FirstOrDefault(window => window.IsActive) ?? MainWindow, question, "Start a card you paused?",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) switch
            {
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                _ => null,
            };
        shell.StartRequests.Changed += (_, _) => Dispatcher.BeginInvoke(shell.Refresh);
        shell.AskNewProject = (name, title) =>
        {
            var window = new NewProjectWindow(name, title) { Owner = MainWindow };

            return window.ShowDialog() == true ? window.Details : null;
        };
        shell.AskText = (title, label, initial) =>
        {
            var window = new TextPromptWindow(title, label, initial) { Owner = MainWindow };

            return window.ShowDialog() == true ? window.Text : null;
        };
        shell.ShowRecordsWindow = model => new RecordsWindow(model) { Owner = MainWindow }.Show();
        shell.ShowProposalWindow = model => new ProposalWindow(model) { Owner = MainWindow }.Show();
        var specWindows = new Dictionary<string, SpecWindow>(StringComparer.OrdinalIgnoreCase);
        shell.ShowSpec = model =>
        {
            var key = $"{model.Shown.Project}/{model.Shown.Slug}";

            if (specWindows.TryGetValue(key, out var open))
            {
                open.Activate();

                return;
            }

            var window = new SpecWindow(model, shell.SettingsStore.Read().SpecWindow,
                placement => shell.SettingsStore.Write(shell.SettingsStore.Read() with { SpecWindow = placement }))
            {
                Owner = MainWindow,
            };
            window.Closed += (_, _) => specWindows.Remove(key);
            specWindows[key] = window;
            window.Show();
        };
        shell.AskObjective = () =>
        {
            var window = new AddObjectiveWindow { Owner = MainWindow };

            return window.ShowDialog() == true ? window.Objective : null;
        };
        shell.AskAddToProject = (projects, slug, chosen) =>
        {
            var window = new AddToProjectWindow(projects, slug, chosen) { Owner = MainWindow };

            return window.ShowDialog() == true ? window.Choice : null;
        };
        MainWindow.Show();
    }

    /// <summary>
    /// Checks the public releases a minute after starting, so the board is up first, and every six
    /// hours after that. GitHub allows sixty unauthenticated calls an hour; this makes four a day.
    /// </summary>
    private void StartUpdateChecks(ShellViewModel shell, SettingsStore settings)
    {
        _releases = new GitHubReleaseFeed(AppVersion.Current, () => settings.Read().IncludePreReleases);

        var updates = new UpdateViewModel(_releases, AppVersion.Current, Path.Combine(DataDirectory, "updates"), msi =>
        {
            UpdateInstaller.Start(msi, Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Theseus.exe"),
                Environment.ProcessId);
            MainWindow?.Close();
        });

        updates.Said = shell.Warn;
        shell.Updates = updates;

        _updateTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _updateTimer.Tick += async (_, _) =>
        {
            _updateTimer.Interval = TimeSpan.FromHours(6);
            await updates.CheckAsync();
        };
        _updateTimer.Start();
    }

    /// <summary>
    /// Serves a taskbar menu request. Bringing a tab to the front is a window-manager operation, so
    /// this copy of Theseus can do it alone rather than finding the running one and asking it to.
    /// </summary>
    private static void Focus(string sessionId)
    {
        var registry = new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe());
        var session = registry.Read().WithSessionId(sessionId).FirstOrDefault();

        if (session is null)
        {
            TheseusLog.Warning("The taskbar asked for session {SessionId}, which is no longer running", sessionId);
            return;
        }

        var outcome = TerminalFocus.FocusSession(session.Pid, new ConsoleTitleReader().Read(session.Pid));

        TheseusLog.Information("The taskbar asked for session {SessionId}: {Outcome}", sessionId, outcome);
    }

    /// <inheritdoc />
    protected override async void OnExit(ExitEventArgs e)
    {
        TheseusLog.Information("Theseus is closing");

        if (Theseus.Core.Status.TheseusStatusFile.Read() is { } status && status.ProcessId == Environment.ProcessId)
        {
            Theseus.Core.Status.TheseusStatusFile.Write(status with { StoppedUtc = DateTimeOffset.UtcNow });
        }
        _usage?.Dispose();
        _resources?.Dispose();
        _attention?.Dispose();
        _updateTimer?.Stop();
        _releases?.Dispose();

        _locks?.Dispose();

        if (_lockSweep is not null)
        {
            await _lockSweep.DisposeAsync();
        }

        if (_mcp is not null)
        {
            await _mcp.DisposeAsync();
        }
        _watcher?.Dispose();

        if (_launcher is not null)
        {
            await _launcher.DisposeAsync();
        }

        base.OnExit(e);
        TheseusLog.Stop();
    }

    /// <summary>
    /// The version presented to the usage endpoint. It refuses requests that do not identify as
    /// Claude Code, so this is required rather than cosmetic.
    /// </summary>
    private const string ClaudeCodeVersion = "2.1.246";

    /// <summary>
    /// The out-of-date notice for the installed holdout guard, or null; recorded as given, so each newer guard is said once.
    /// Read only: the guard itself is written only by Install the holdout guard.
    /// </summary>
    private static string? HoldoutGuardNotice(SettingsStore settings)
    {
        try
        {
            var notice = new HoldoutGuardInstallation(HoldoutGuardInstallation.UserConfigFolder()).State()
                .OutOfDateNotice(settings.Read().HoldoutGuardNoticeFor);

            if (notice is not null)
            {
                settings.Write(settings.Read() with { HoldoutGuardNoticeFor = HoldoutGuardBundle.Version });
            }

            return notice;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string LauncherPath => LauncherKit.Prepare(
        Path.Combine(AppContext.BaseDirectory, "Theseus.Launcher.exe"),
        Path.Combine(DataDirectory, "launcher"));
}
