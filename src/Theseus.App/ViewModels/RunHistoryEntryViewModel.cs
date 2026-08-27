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

    /// <summary>
    /// How long the run lasted, how long ago it started if it is still going, or nothing measurable.
    /// A run that ended unwatched has no ending moment, and reading that as "still running" would
    /// describe an afternoon-old run as work in progress.
    /// </summary>
    public string Duration
    {
        get
        {
            if (_run.Duration is not { } elapsed)
            {
                return _run.HasEnded
                    ? "length unknown"
                    : $"started {RelativeTimeConverter.Format(_run.StartedUtc, DateTimeOffset.UtcNow)} ago";
            }

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
        SessionExitReason.CrashedOrKilled => _run.ExitCode is { } code ? $"exit {code}" : "killed",
        SessionExitReason.NeverStarted => "never started",
        SessionExitReason.TheseusWasDown => "outcome unknown",
        _ => _run.EndedUtc is null ? "running" : "ended",
    };
}
