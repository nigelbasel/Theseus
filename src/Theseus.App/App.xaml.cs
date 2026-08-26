using System.IO;
using System.Windows;
using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
using Theseus.Core.Colour;
using Theseus.Core.Diagnostics;
using Theseus.Core.Launching;
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

    /// <summary>Where Theseus keeps the state the plan files have no room for.</summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Theseus");

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(DataDirectory);
        TheseusLog.Start(Path.Combine(DataDirectory, "logs"));

        DispatcherUnhandledException += (_, args) =>
        {
            TheseusLog.Error(args.Exception, "An unhandled exception reached the dispatcher");
            args.Handled = false;
        };

        var settings = new SettingsStore(Path.Combine(DataDirectory, "settings.json"));
        var current = settings.Read();

        Directory.CreateDirectory(current.PlanDirectory);

        var plans = new PlanStore(current.PlanDirectory);
        var board = new BoardViewModel(plans, new BoardStore(Path.Combine(DataDirectory, "board.json")));
        var sessions = new SessionsViewModel(
            new SessionRegistryReader(SessionRegistryReader.DefaultDirectory, new ProcessProbe()),
            new ConsoleTitleReader());

        var sink = new BoardLaunchEventSink(board, Dispatcher);

        var runs = new RunLog(Path.Combine(DataDirectory, "runs.jsonl"));

        _launcher = new LaunchCoordinator(
            sink,
            new CachedTerminalResolver(new TerminalResolver(current.TerminalExecutablePath), new SystemClock()),
            new ClaudeLocator(current.ClaudeExecutablePath),
            new SessionIdFactory(),
            runs,
            new SystemClock(),
            ShimProtocol.DefaultPipeName,
            LauncherPath,
            current.TerminalWindowName,
            sink.Expect);

        var shell = new ShellViewModel(board, sessions, settings, _launcher, plans, runs, new TabColourAllocator());
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

        MainWindow = new MainWindow { DataContext = shell };
        MainWindow.Show();
    }

    /// <inheritdoc />
    protected override async void OnExit(ExitEventArgs e)
    {
        TheseusLog.Information("Theseus is closing");
        _usage?.Dispose();
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

    private static string LauncherPath =>
        Path.Combine(AppContext.BaseDirectory, "Theseus.Launcher.exe");
}
