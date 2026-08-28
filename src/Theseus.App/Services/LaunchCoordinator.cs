using System.Diagnostics;
using System.IO;
using Theseus.Core.Abstractions;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Messaging;
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
        _ticketIssued = ticketIssued;

        _pipe = new LaunchPipeServer(pipeName, sink, clock);
        _pipe.Start();
    }

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

    /// <summary>Opens a tab for one workstream and records the attempt.</summary>
    /// <returns>The session identifier the tab was started under, or null if it could not start.</returns>
    public string? Launch(Workstream workstream, string? planPath = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (_claude.Locate() is not { } claudePath
            || _terminals.Resolve() is not { Source: not TerminalSource.None } terminal)
        {
            return null;
        }

        var resume = !string.IsNullOrWhiteSpace(workstream.LastSessionId)
                     && TranscriptExists(workstream.LastSessionId);

        var mode = resume ? LaunchMode.Resume : LaunchMode.FirstRun;
        var sessionId = resume ? workstream.LastSessionId! : _sessionIds.Create();

        var settings = _settings.Read();
        var handoff = HandoffPath.For(workstream);
        var opening = SessionOpening.For(
            settings.SessionPreamble,
            settings.ContinuePrompt,
            workstream,
            SessionOpening.HasHistory(workstream, File.Exists(handoff)),
            handoff,
            File.Exists(handoff),
            planPath);


        var request = new WorkstreamLaunchRequest(workstream, Guid.NewGuid().ToString("N"), sessionId,
            mode, claudePath, terminal.ExecutablePath, _launcherPath, _pipeName,
            settings.TerminalWindowName, opening);

        var plan = WorkstreamLaunchPlanner.Plan(request, DateTimeOffset.UtcNow.AddMinutes(2));

        var runId = Guid.NewGuid();

        _ticketIssued?.Invoke(plan.Ticket.Ticket, workstream.Key, runId);
        _pipe.Register(plan.Ticket);

        try
        {
            Start(plan);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _sink.TicketRefused(plan.Ticket.Ticket, $"The terminal would not start: {exception.Message}");

            return null;
        }

        _runs.Append(new SessionRun(runId, sessionId, workstream.Key, workstream.WorkingDirectory,
            null, resume ? SessionLaunchMode.Resume : SessionLaunchMode.FirstRun, workstream.Prompt,
            workstream.Title, workstream.ColorHex, DateTimeOffset.UtcNow, null, null, null, null,
            SessionExitReason.Unknown));

        return sessionId;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _pipe.DisposeAsync();

    /// <summary>
    /// Whether a transcript exists for a session. The directory is never computed from the working
    /// directory: it is pinned to wherever the session was first created, which may no longer match.
    /// </summary>
    public static bool TranscriptExists(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        var projects = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");

        return Directory.Exists(projects)
               && Directory.EnumerateFiles(projects, $"{sessionId}.jsonl", SearchOption.AllDirectories).Any();
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

        Process.Start(startInfo);
    }
}
