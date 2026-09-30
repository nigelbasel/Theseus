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
/// it was first missed is the closest observation available. A run already over before this
/// process started watching cannot be dated at all - so it is recorded as ended with no moment,
/// which is the honest answer and keeps a fabricated duration out of any total.
///
/// Two things make the evidence weaker than it looks. A resume gives a new run the same session
/// identifier as the last one, so "this session was seen live" says nothing about which run's
/// process that was: a registry entry belongs to the newest run issued before its process started.
/// And Claude Code rewrites an entry every time its status changes, so a single read can find the
/// file half-written and report the session gone; on 2026-09-30 that ended a run twenty-two seconds
/// into a session that ran on all morning. A run has to be missed twice running before it is over.
/// </summary>
public sealed class RunEndings
{
    /// <summary>
    /// How far a registry entry's process may have started before the run it belongs to was issued.
    /// Both clocks are this machine's, so this covers rounding, not drift.
    /// </summary>
    public static readonly TimeSpan StartTolerance = TimeSpan.FromSeconds(5);

    private readonly DateTimeOffset _watchingSince;
    private readonly HashSet<Guid> _seenLive = [];
    private readonly Dictionary<Guid, DateTimeOffset> _firstMissed = [];

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

        var runs = log.Read();
        var running = RunsWithAProcess(runs, live);
        var wrote = false;

        foreach (var run in runs.Where(run => !run.HasEnded))
        {
            if (running.Contains(run.Id))
            {
                _seenLive.Add(run.Id);
                _firstMissed.Remove(run.Id);

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
    /// The runs whose process is in the registry. Each live entry is credited to the newest run of
    /// its session that had been issued by the time the process started; an older run of the same
    /// session, left open when its own process went, gets nothing from its successor.
    /// </summary>
    private static HashSet<Guid> RunsWithAProcess(IReadOnlyList<SessionRun> runs, IReadOnlyList<LiveSession> live)
    {
        var running = new HashSet<Guid>();

        foreach (var session in live)
        {
            var owner = runs
                .Where(run => string.Equals(run.ClaudeSessionId, session.SessionId, StringComparison.OrdinalIgnoreCase))
                .Where(run => run.StartedUtc <= session.StartedUtc + StartTolerance)
                .OrderByDescending(run => run.StartedUtc)
                .FirstOrDefault();

            if (owner is not null)
            {
                running.Add(owner.Id);
            }
        }

        return running;
    }

    /// <summary>
    /// The ending this run has earned, or none. A run this process watched and then lost twice in a
    /// row ended when it was first lost. A run that predates the watching ended at an unknowable
    /// moment. A run started during this lifetime that has not registered yet has earned nothing:
    /// the gap between opening a tab and the session appearing is not a death, and the pipe owns
    /// those runs.
    /// </summary>
    private RunEnd? Ending(SessionRun run, DateTimeOffset now)
    {
        if (_seenLive.Contains(run.Id))
        {
            if (_firstMissed.TryAdd(run.Id, now))
            {
                return null;
            }

            return new RunEnd(run.Id, _firstMissed[run.Id], null, SessionExitReason.CrashedOrKilled);
        }

        return run.StartedUtc < _watchingSince
            ? new RunEnd(run.Id, null, null, SessionExitReason.TheseusWasDown)
            : null;
    }
}
