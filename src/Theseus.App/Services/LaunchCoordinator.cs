using System.Diagnostics;
using System.IO;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Messaging;
using Theseus.Core.Running;
using Theseus.Core.Settings;
using Theseus.Core.Stopping;
using Theseus.Core.Terminal;

namespace Theseus.App.Services;

/// <summary>
/// Opens tabs. It mints the session identifier before the terminal starts, which is what makes a
/// session resumable tomorrow, and records every attempt whether or not it succeeds.
/// </summary>
public sealed class LaunchCoordinator : IAsyncDisposable
{
    private readonly LaunchPipeServer _pipe;
    private readonly ILaunchEventSink _sink;
    private readonly ITerminalResolver _terminals;
    private readonly IClaudeLocator _claude;
    private readonly ISessionIdFactory _sessionIds;
    private readonly IRunLog _runs;
    private readonly string _pipeName;
    private readonly string _launcherPath;
    private readonly ISettingsStore _settings;
    private readonly ISystemClock _clock;
    private readonly Action<string, string, Guid>? _ticketIssued;

    /// <summary>Creates the coordinator and starts listening for shims.</summary>
    public LaunchCoordinator(
        ILaunchEventSink sink,
        ITerminalResolver terminals,
        IClaudeLocator claude,
        ISessionIdFactory sessionIds,
        IRunLog runs,
        ISystemClock clock,
        string pipeName,
        string launcherPath,
        ISettingsStore settings,
        Action<string, string, Guid>? ticketIssued = null)
    {
        _sink = sink;
        _terminals = terminals;
        _claude = claude;
        _sessionIds = sessionIds;
        _runs = runs;
        _pipeName = pipeName;
        _launcherPath = launcherPath;
        _settings = settings;
        _clock = clock;
        _ticketIssued = ticketIssued;

        _pipe = new LaunchPipeServer(pipeName, sink, clock);
        _pipe.Start();
    }

    /// <summary>The launches whose runs have not ended, so one card started twice never opens a second session.</summary>
    public LaunchesInFlight InFlight { get; } = new();

    /// <summary>Whether everything a launch needs was found on this machine.</summary>
    public bool CanLaunch => _claude.Locate() is not null
                             && _terminals.Resolve().Source != TerminalSource.None
                             && File.Exists(_launcherPath);

    /// <summary>Why a launch is impossible, when it is.</summary>
    public string? Impediment
    {
        get
        {
            if (_claude.Locate() is null)
            {
                return "Claude Code could not be found. Set its path in settings.";
            }

            if (_terminals.Resolve().Source == TerminalSource.None)
            {
                return "Windows Terminal could not be found.";
            }

            return File.Exists(_launcherPath)
                ? null
                : $"The launcher shim is missing from {_launcherPath}.";
        }
    }

    /// <summary>
    /// Makes the <c>--mcp-config</c> value for a session, once the session time server is listening.
    /// Null means it is not, and sessions start without it rather than pointed at nothing.
    /// </summary>
    public Func<string, string?>? McpConfigFor { get; set; }

    /// <summary>The <c>--settings</c> every session is launched with, or null for none.</summary>
    public string? SessionSettings { get; set; }

    /// <summary>
    /// The settings a card of an unattended project is given instead of <see cref="SessionSettings"/>: the hook also on the
    /// question tool and on Stop (2.4 W2). Null to give every card the same.
    /// </summary>
    public string? UnattendedSessionSettings { get; set; }

    /// <summary>Whether a card's project is unattended, asked at each start from the settings as they are then; null for none.</summary>
    public Func<Workstream, bool>? UnattendedFor { get; set; }

    private string? SettingsFor(Workstream workstream) =>
        UnattendedSessionSettings is not null && UnattendedFor?.Invoke(workstream) == true ? UnattendedSessionSettings : SessionSettings;

    /// <summary>Marks a session as Theseus's at launch, so its hook tells it when Theseus stops or restarts.</summary>
    public Action<string>? Launched { get; set; }

    /// <summary>Told the card's key at each launch, so what its last session said through now_at is forgotten (1.1f E24).</summary>
    public Action<string>? LaunchedCard { get; set; }

    /// <summary>The related-workstreams paragraph a fresh session of a workstream is given, by key.</summary>
    public Func<string, string?>? RelatedFor { get; set; }

    /// <summary>
    /// The branch rules the owner granted a card's project, as text naming its primary branch now, or null (1.1e AC 12b):
    /// asked at each start, so turning the grant off changes the next start.
    /// </summary>
    public Func<Workstream, string?>? BranchRulesFor { get; set; }

    /// <summary>
    /// The projects that read now, asked at each start, so a card that joined a project mid-session opens in the
    /// project's window at its next start (project-view AC 20). Null or empty: every card opens where it always did.
    /// </summary>
    public Func<IReadOnlySet<string>>? ReadableProjects { get; set; }

    /// <summary>What changed in a workstream's links since a moment, for a resumed session.</summary>
    public Func<string, DateTimeOffset, string?>? RelatedSince { get; set; }

    /// <summary>Why the last <see cref="Launch"/> failed to open its tab, when the terminal refused it.</summary>
    public string? LastRefusal { get; private set; }

    /// <summary>The breaker or limit that refused the last <see cref="Launch"/>, for the board to say where Start was pressed.</summary>
    public StartBlock? LastBlock { get; private set; }

    /// <summary>Opens a tab for one workstream and records the attempt.</summary>
    /// <returns>The session identifier the tab was started under, or null if it could not start.</returns>
    /// <param name="startMode">
    /// Resume the last conversation or start fresh from the handoff. Null means the board's default.
    /// </param>
    /// <param name="reason">What asked for the start, recorded with a headless run.</param>
    /// <param name="chosenOnTheCard">Whether the owner picked <paramref name="startMode"/> from the card's own ▾ menu.</param>
    public string? Launch(Workstream workstream, string? planPath = null, StartMode? startMode = null, RunReason reason = RunReason.Owner,
        bool chosenOnTheCard = false)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        LastRefusal = null;
        LastBlock = null;

        // A tab waiting at Claude Code's question about trusting its folder has registered no session yet, so the board's
        // own check would start the card again (git-22, 2026-11-23).
        if (InFlight.IsInFlight(workstream.Key))
        {
            LastRefusal = $"{workstream.Title} is already starting: its tab is open, perhaps asking whether to trust its folder. Answer it there.";
            TheseusLog.Information("Refused a second start of {Workstream}: its launch is still in flight", workstream.Key);

            return null;
        }

        if (_claude.Locate() is not { } claudePath)
        {
            return null;
        }

        var settings = _settings.Read();
        var headless = Headless is not null && RunModeChoice.For(workstream, settings.ProjectRunModes) == RunMode.Headless;
        var terminal = headless ? null : _terminals.Resolve();

        if (!headless && terminal is not { Source: not TerminalSource.None })
        {
            return null;
        }

        if (headless && (Headless!.Holds(workstream.Key) ? $"{workstream.Title} already has a headless run going or queued." : Headless.Refusal(workstream)) is { } refusal)
        {
            LastRefusal = refusal;
            LastBlock = Headless.Holds(workstream.Key) ? null : Headless.Block(workstream);
            TheseusLog.Information("Refused a headless start of {Workstream}: {Refusal}", workstream.Key, refusal);

            return null;
        }
        var requested = startMode ?? settings.DefaultStartMode;

        var mode = StartModeChoice.For(requested,
            !string.IsNullOrWhiteSpace(workstream.LastSessionId) && TranscriptExists(workstream.LastSessionId),
            workstream.Role, chosenOnTheCard);
        var resume = mode == LaunchMode.Resume;
        var sessionId = resume ? workstream.LastSessionId! : _sessionIds.Create();

        var handoff = HandoffPath.For(workstream);
        var fullOpening = SessionOpening.For(
            settings.SessionPreamble,
            settings.ContinuePrompt,
            workstream,
            SessionOpening.HasHistory(workstream, File.Exists(handoff)),
            handoff,
            File.Exists(handoff),
            planPath,
            settings.IdentityPrompt,
            RelatedFor?.Invoke(workstream.Key));

        var rules = BranchRulesFor?.Invoke(workstream);
        var toldBefore = settings.CardsToldBranchRules.Contains(workstream.Key, StringComparer.OrdinalIgnoreCase);

        // A resume keeps its conversation, so it opens with one line. The full opening still
        // travels with it, for the shim to use if the resume dies at once and is reminted fresh.
        string opening;
        (fullOpening, opening) = Theseus.Core.Projects.ProjectBranchRules.Openings(fullOpening,
            resume ? SessionOpening.ForResume(workstream, _clock.UtcNow, handoff, RelatedSinceLastRun(workstream.Key)) : null,
            rules, toldBefore);

        // Which cards were given the rules, so a later resume without them is told they are withdrawn (1.1e AC 12b).
        if ((rules is not null) != toldBefore)
        {
            var told = settings.CardsToldBranchRules.Where(key => !string.Equals(key, workstream.Key, StringComparison.OrdinalIgnoreCase));
            _settings.Write(_settings.Read() with { CardsToldBranchRules = rules is null ? [.. told] : [.. told, workstream.Key] });
        }

        if (headless)
        {
            return LaunchHeadless(workstream, sessionId, mode, claudePath, opening, resume ? fullOpening : null, handoff, settings, reason);
        }

        // Which window this tab joins is the whole of grouping: same name, same window; a name
        // nothing is using yet makes a new one. Nothing else has to exist for sessions to be grouped.
        var windowName = WindowGroupName.For(settings.TerminalWindowName, workstream, settings.AreaWindows, ReadableProjects?.Invoke());

        var request = new WorkstreamLaunchRequest(workstream, Guid.NewGuid().ToString("N"), sessionId,
            mode, claudePath, terminal!.ExecutablePath, _launcherPath, _pipeName,
            windowName, opening, McpConfigFor?.Invoke(sessionId), resume ? fullOpening : null)
        {
            Settings = SettingsFor(workstream),
        };

        var plan = WorkstreamLaunchPlanner.Plan(request, DateTimeOffset.UtcNow.AddMinutes(2));

        var runId = Guid.NewGuid();

        // Every launch, said out loud. The run log records that one happened but not what asked for
        // it, and on 2026-09-08 one press of Restart produced two launches forty seconds apart with
        // nothing to show which caller made either. One line here turns that from a mystery into a
        // reading.
        TheseusLog.Information(
            "Launching {Workstream} as {Mode} ({Requested} asked for) into window {Window}: ticket {Ticket}, run {Run}, session {Session}",
            workstream.Key, mode, requested, windowName, plan.Ticket.Ticket, runId, sessionId);

        _ticketIssued?.Invoke(plan.Ticket.Ticket, workstream.Key, runId);
        _pipe.Register(plan.Ticket);
        InFlight.Began(workstream.Key, plan.Ticket.Ticket);

        try
        {
            plan = StartWithFallback(plan, workstream.Key);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _pipe.Withdraw(plan.Ticket.Ticket);
            TheseusLog.Warning(exception, "The terminal {Terminal} would not start for {Workstream}",
                plan.TerminalExecutablePath, workstream.Key);
            LastRefusal = StartNotice.Refusal(workstream.Title, exception.Message);
            InFlight.Ended(plan.Ticket.Ticket);
            _sink.TicketRefused(plan.Ticket.Ticket, $"The terminal would not start: {exception.Message}");

            return null;
        }

        Launched?.Invoke(sessionId);
        LaunchedCard?.Invoke(workstream.Key);

        _runs.Append(new SessionRun(runId, sessionId, workstream.Key, workstream.WorkingDirectory,
            null, resume ? SessionLaunchMode.Resume : SessionLaunchMode.FirstRun, workstream.Prompt,
            workstream.Title, workstream.ColorHex, DateTimeOffset.UtcNow, null, null, null, null,
            SessionExitReason.Unknown)
        {
            WindowName = windowName,
        });

        return sessionId;
    }

    /// <summary>
    /// Runs headless the cards whose mode says so (Nigel, 2026-11-24); null runs every card in a tab, as before.
    /// </summary>
    public HeadlessRunner? Headless { get; set; }

    /// <summary>The project's current piece of work for a card, recorded with its headless runs, or null.</summary>
    public Func<Workstream, string?>? PieceFor { get; set; }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _pipe.DisposeAsync();

    // Built exactly as a tab's launch is, so a headless run keeps the Theseus tools, its folders, its resume and its
    // handoff; only the process differs.
    private string LaunchHeadless(Workstream workstream, string sessionId, LaunchMode mode, string claudePath, string opening,
        string? openingIfReminted, string handoff, TheseusSettings settings, RunReason reason)
    {
        var request = new WorkstreamLaunchRequest(workstream, Guid.NewGuid().ToString("N"), sessionId, mode, claudePath,
            string.Empty, _launcherPath, _pipeName, string.Empty, opening, McpConfigFor?.Invoke(sessionId), openingIfReminted)
        {
            Settings = SettingsFor(workstream),
        };

        var spec = WorkstreamLaunchPlanner.Plan(request, DateTimeOffset.UtcNow).Ticket.Spec;
        var stopText = StopRequest.For(settings.StopPrompt, new StopTarget(workstream.Title, handoff));
        var runId = Guid.NewGuid();

        TheseusLog.Information("Launching {Workstream} headless as {Mode}: run {Run}, session {Session}", workstream.Key, mode, runId, sessionId);

        Launched?.Invoke(sessionId);
        LaunchedCard?.Invoke(workstream.Key);

        _runs.Append(new SessionRun(runId, sessionId, workstream.Key, workstream.WorkingDirectory,
            null, mode == LaunchMode.Resume ? SessionLaunchMode.Resume : SessionLaunchMode.FirstRun, workstream.Prompt,
            workstream.Title, workstream.ColorHex, DateTimeOffset.UtcNow, null, null, null, null,
            SessionExitReason.Unknown));

        Headless!.Submit(new HeadlessSubmission(workstream, spec, reason, stopText) { Piece = PieceFor?.Invoke(workstream) });

        return sessionId;
    }

    private string? RelatedSinceLastRun(string key)
    {
        if (RelatedSince is null)
        {
            return null;
        }

        var last = _runs.Read()
            .Where(run => string.Equals(run.WorkstreamKey, key, StringComparison.OrdinalIgnoreCase))
            .Select(run => (DateTimeOffset?)run.StartedUtc)
            .Max();

        return last is { } started ? RelatedSince(key, started) : null;
    }

    /// <summary>
    /// Whether a transcript exists for a session. The directory is never computed from the working
    /// directory: it is pinned to wherever the session was first created, which may no longer match.
    /// </summary>
    public static bool TranscriptExists(string? sessionId) => TranscriptLocator.Find(sessionId) is not null;

    /// <summary>
    /// Starts the terminal, and when Windows refuses the package's own executable starts the
    /// <c>wt.exe</c> alias instead with the same arguments, remembering the refusal so later launches
    /// go straight to the alias. Reported as a refusal only if the alias fails too.
    /// </summary>
    private WorkstreamLaunchPlan StartWithFallback(WorkstreamLaunchPlan plan, string workstream)
    {
        try
        {
            Start(plan);

            return plan;
        }
        catch (System.ComponentModel.Win32Exception exception)
            when (RefusedTerminals.IsPackageRefusal(exception, plan.TerminalExecutablePath) && File.Exists(TerminalResolver.AliasPath))
        {
            TheseusLog.Warning(
                "Windows refused to start {Refused} for {Workstream} ({Reason}); starting the wt.exe alias at {Alias} instead, and using it for the rest of this run",
                plan.TerminalExecutablePath, workstream, exception.Message, TerminalResolver.AliasPath);

            RefusedTerminals.Shared.Add(plan.TerminalExecutablePath);
            (_terminals as CachedTerminalResolver)?.Forget();

            var retry = plan with { TerminalExecutablePath = TerminalResolver.AliasPath };
            Start(retry);

            return retry;
        }
    }

    private static void Start(WorkstreamLaunchPlan plan)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = plan.TerminalExecutablePath,
            UseShellExecute = false,
        };

        foreach (var argument in plan.TerminalArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var terminal = Process.Start(startInfo);

        TheseusLog.Debug("Started {Terminal} (process {Process}) with {Arguments}", plan.TerminalExecutablePath,
            terminal?.Id, string.Join(" | ", plan.TerminalArguments));
    }
}
