using Theseus.App.Converters;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One past launch, as the drawer lists it.</summary>
public sealed class RunHistoryEntryViewModel
{
    private readonly SessionRun _run;

    /// <summary>Creates a row for a recorded run.</summary>
    public RunHistoryEntryViewModel(SessionRun run) => _run = run;

    /// <summary>The first characters of the session identifier, which is enough to tell runs apart.</summary>
    public string ShortSessionId => _run.ClaudeSessionId.Length >= 8
        ? _run.ClaudeSessionId[..8]
        : _run.ClaudeSessionId;

    /// <summary>When the run started, in local time.</summary>
    public string Started => _run.StartedUtc.ToLocalTime().ToString("ddd d MMM HH:mm");

    /// <summary>How long the run lasted, or how long ago it started if it has not ended.</summary>
    public string Duration
    {
        get
        {
            if (_run.EndedUtc is not { } ended)
            {
                return $"started {RelativeTimeConverter.Format(_run.StartedUtc, DateTimeOffset.UtcNow)} ago";
            }

            var elapsed = ended - _run.StartedUtc;

            return elapsed.TotalHours >= 1
                ? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m"
                : $"{(int)elapsed.TotalMinutes}m";
        }
    }

    /// <summary>How the run was started and how it ended.</summary>
    public string Outcome => _run.LaunchMode switch
    {
        SessionLaunchMode.Resume => $"resumed · {Ending}",
        SessionLaunchMode.RemintedAfterResumeFailure => $"reminted · {Ending}",
        _ => $"new · {Ending}",
    };

    /// <summary>The identifier a restart of this run would resume.</summary>
    public string SessionId => _run.ClaudeSessionId;

    private string Ending => _run.ExitReason switch
    {
        SessionExitReason.ClosedCleanly => "closed cleanly",
        SessionExitReason.CrashedOrKilled => $"exit {_run.ExitCode}",
        SessionExitReason.NeverStarted => "never started",
        SessionExitReason.TheseusWasDown => "outcome unknown",
        _ => _run.EndedUtc is null ? "running" : "ended",
    };
}
