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

namespace Theseus.App;

/// <summary>Composition root. Everything the window needs is built here and nowhere else.</summary>
public partial class App : Application
{
    private LaunchCoordinator? _launcher;
    private SessionRegistryWatcher? _watcher;
    private UsageViewModel? _usage;
    private AttentionSampler? _attention;
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

        var chains = new ChainsViewModel(
            new LinkBook(new LinkStateStore(Path.Combine(DataDirectory, "links.json")), new SystemClock()),
            () => [.. shell.Board.Cards],
            () => settings.Read().BriefFolderName,
            shell.Refresh,
            key => shell.SelectCardCommand.Execute(shell.Board.Card(key)));

        shell.Chains = chains;
        _launcher.RelatedFor = chains.RelatedParagraph;
        _launcher.RelatedSince = chains.RelatedSince;

        if (ChildSessionCheck.Warning() is { } warning)
        {
            TheseusLog.Warning("{Warning}", warning);
            shell.Warn(warning);
        }
        shell.Refresh();
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
                locks: locks),
            Theseus.Core.Mcp.TheseusMcp.DefaultPort,
            new LockHttpApi(locks, () => DateTimeOffset.UtcNow));

        if (_mcp.Start() && _launcher is not null)
        {
            var port = _mcp.Port;
            _launcher.McpConfigFor = sessionId => Theseus.Core.Mcp.TheseusMcp.ConfigJson(port, sessionId);
        }

        SessionJumpList.Show(this, sessions.Snapshot.Live);
        sessions.Refreshed += (_, _) => SessionJumpList.Show(this, sessions.Snapshot.Live);

        MainWindow = new MainWindow { DataContext = shell };
        MainWindow.Show();
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
        _usage?.Dispose();
        _attention?.Dispose();

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

    private static string LauncherPath => LauncherKit.Prepare(
        Path.Combine(AppContext.BaseDirectory, "Theseus.Launcher.exe"),
        Path.Combine(DataDirectory, "launcher"));
}
