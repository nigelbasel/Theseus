using Theseus.Core.Abstractions;
using Theseus.Core.Launching;
using Theseus.Core.Terminal;

namespace Theseus.Launcher;

/// <summary>
/// Runs Claude Code in the shim's own console and decides what to do when it fails. A resume that
/// dies almost immediately means the transcript is gone, not that the work is: the session is
/// restarted once under a fresh identifier so the tab is never left holding a stale identifier.
/// </summary>
public sealed class ClaudeProcessRunner
{
    /// <summary>How quickly a resume must fail to be treated as an unusable identifier rather than a real session ending.</summary>
    public static readonly TimeSpan RemintWindow = TimeSpan.FromSeconds(5);

    private readonly IProcessLauncher _launcher;
    private readonly ISessionIdFactory _sessionIds;
    private readonly ISystemClock _clock;

    /// <summary>Creates the runner.</summary>
    public ClaudeProcessRunner(IProcessLauncher launcher, ISessionIdFactory sessionIds, ISystemClock clock)
    {
        _launcher = launcher;
        _sessionIds = sessionIds;
        _clock = clock;
    }

    /// <summary>Runs the session described by <paramref name="spec"/> to completion.</summary>
    public ClaudeRunResult Run(LaunchSpec spec, ILaunchReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(reporter);

        var (exitCode, elapsed) = RunOnce(spec, reporter);
        var duration = elapsed;

        if (spec.Mode == LaunchMode.Resume && exitCode != 0 && elapsed < RemintWindow)
        {
            var reminted = spec with
            {
                SessionId = _sessionIds.Create(),
                Mode = LaunchMode.FirstRun,
                Prompt = spec.PromptIfReminted ?? spec.Prompt,
                PromptIfReminted = null,
            };

            reporter.Reminted(spec.SessionId, reminted.SessionId,
                $"Resume exited {exitCode} after {elapsed.TotalMilliseconds:F0} ms.");

            (exitCode, elapsed) = RunOnce(reminted, reporter);
            duration += elapsed;
            spec = reminted;
        }

        reporter.Exited(spec.SessionId, exitCode, (long)duration.TotalMilliseconds);

        return new ClaudeRunResult(spec.SessionId, exitCode, exitCode != 0);
    }

    private (int ExitCode, TimeSpan Elapsed) RunOnce(LaunchSpec spec, ILaunchReporter reporter)
    {
        var request = new ProcessStartRequest(
            spec.ClaudeExecutablePath,
            ClaudeArgumentBuilder.Build(spec),
            spec.WorkingDirectory);

        var startedUtc = _clock.UtcNow;

        using var handle = _launcher.Start(request);
        reporter.Started(spec.SessionId, handle.ProcessId);

        var exitCode = handle.WaitForExit();

        return (exitCode, _clock.UtcNow - startedUtc);
    }
}
