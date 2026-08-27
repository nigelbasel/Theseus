using Theseus.Core.Abstractions;
using Theseus.Core.Sessions;

namespace Theseus.Core.Domain;

/// <summary>
/// Gives an ending to runs nothing was left to report one for.
///
/// A shim reports its session's exit over the pipe, which covers every ordinary ending. It cannot
/// cover the ones where the shim itself was taken away: a machine shut down, a terminal window
/// closed, a session killed. Those runs would otherwise stay open in the log for ever, and an open
/// run reads as a run still going.
///
/// The registry is the evidence. A session that was there and is now gone has ended, and the moment
/// it was seen to vanish is the closest observation available. A run already over before this
/// process started watching cannot be dated at all - so it is recorded as ended with no moment,
/// which is the honest answer and keeps a fabricated duration out of any total.
/// </summary>
public sealed class RunEndings
{
    private readonly DateTimeOffset _watchingSince;
    private readonly HashSet<string> _seenLive = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the sweep, told when this application began watching the registry.</summary>
    public RunEndings(DateTimeOffset watchingSince) => _watchingSince = watchingSince;

    /// <summary>
    /// Records any endings the evidence justifies and returns the runs with them folded in. Safe to
    /// call on every registry change: a run already ended is never revisited, so the log does not
    /// grow an ending per debounce tick.
    /// </summary>
    public IReadOnlyList<SessionRun> Reconcile(
        IRunLog log,
        IReadOnlyList<LiveSession> live,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(live);

        var running = live.Select(session => session.SessionId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var runs = log.Read();
        var wrote = false;

        foreach (var run in runs.Where(run => !run.HasEnded))
        {
            if (running.Contains(run.ClaudeSessionId))
            {
                _seenLive.Add(run.ClaudeSessionId);

                continue;
            }

            if (Ending(run, now) is not { } end)
            {
                continue;
            }

            log.AppendEnd(end);
            wrote = true;
        }

        return wrote ? log.Read() : runs;
    }

    /// <summary>
    /// The ending this run has earned, or none. A run this process watched and then lost ended when
    /// it was lost. A run that predates the watching ended at an unknowable moment. A run started
    /// during this lifetime that has not registered yet has earned nothing: the gap between opening
    /// a tab and the session appearing is not a death, and the pipe owns those runs.
    /// </summary>
    private RunEnd? Ending(SessionRun run, DateTimeOffset now)
    {
        if (_seenLive.Contains(run.ClaudeSessionId))
        {
            return new RunEnd(run.Id, now, null, SessionExitReason.CrashedOrKilled);
        }

        return run.StartedUtc < _watchingSince
            ? new RunEnd(run.Id, null, null, SessionExitReason.TheseusWasDown)
            : null;
    }
}
