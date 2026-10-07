using System.Globalization;
using Theseus.Core.Abstractions;

namespace Theseus.Core.Locks;

/// <summary>
/// The machine's locks and their queues, held by Theseus (Nigel, 2026-10-06). Sessions and scripts ask
/// for a lock and wait their turn; only Nigel, from the board, reorders, cancels or releases someone
/// else's. Every change is saved, so a restart picks up exactly where it stopped, and every grant and
/// release is recorded, so what contention costs can be measured.
///
/// A running holder is never interrupted by anything here except Nigel's own release. Stopping a test
/// suite part way leaves its containers behind, which is the failure the lock exists to prevent.
/// </summary>
public sealed class LockBook
{
    /// <summary>How long a grant waits for its process to take it up before it passes to the next in line.</summary>
    public static readonly TimeSpan Reservation = TimeSpan.FromMinutes(2);

    /// <summary>How long a waiting process may go without asking before it is taken out of the queue.</summary>
    public static readonly TimeSpan WaiterGrace = TimeSpan.FromMinutes(5);

    private const int SessionMissesBeforeGone = 2;
    private const int EndedRemembered = 500;

    private readonly Lock _gate = new();
    private readonly List<LockTicket> _tickets;
    private readonly Dictionary<string, string> _ended = new(StringComparer.Ordinal);
    private readonly Queue<string> _endedOrder = new();
    private readonly Dictionary<string, int> _sessionMisses = new(StringComparer.Ordinal);
    private readonly LockStateStore _store;
    private readonly LockHistory _history;
    private readonly ILockLiveness _liveness;
    private readonly ISystemClock _clock;
    private readonly IProcessTree? _tree;
    private readonly IOrphanCleaner? _cleaner;

    /// <summary>
    /// Opens the book from its saved state. Waiters are given a fresh grace period and unattached grants
    /// a fresh reservation, because nobody could reach Theseus while it was not running.
    /// </summary>
    /// <param name="tree">Finds the test hosts under a test run's holder, so they can be cleared if it dies.</param>
    /// <param name="cleaner">Clears what a dead test run's holder left running before the next is granted.</param>
    public LockBook(
        LockStateStore store,
        LockHistory history,
        ILockLiveness liveness,
        ISystemClock clock,
        IProcessTree? tree = null,
        IOrphanCleaner? cleaner = null)
    {
        _store = store;
        _history = history;
        _liveness = liveness;
        _clock = clock;
        _tree = tree;
        _cleaner = cleaner;

        var now = clock.UtcNow;

        _tickets = store.Load()
            .Where(ticket => LockNames.Find(ticket.Lock) is not null)
            .Select(ticket => Resumed(ticket, now))
            .ToList();
    }

    /// <summary>Raised after any change to a lock or a queue, on whichever thread made it.</summary>
    public event EventHandler? Changed;

    /// <summary>Every ticket, holders and waiters, in queue order.</summary>
    public IReadOnlyList<LockTicket> Snapshot()
    {
        lock (_gate)
        {
            return [.. _tickets];
        }
    }

    /// <summary>
    /// Joins a queue, or, when the same requester asks again, finds its existing ticket so it keeps its
    /// place. Returns at once with the ticket's standing; it never waits.
    /// </summary>
    public LockAnswer Request(LockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (LockNames.Find(request.Lock) is not { } name)
        {
            return new LockAnswer(LockStatus.Refused, $"There is no lock called '{request.Lock}'. The locks are: {string.Join(", ", LockNames.All)}.");
        }

        if (string.IsNullOrWhiteSpace(request.Who) || string.IsNullOrWhiteSpace(request.Purpose))
        {
            return new LockAnswer(LockStatus.Refused, "A lock request needs who is asking and what it is for.");
        }

        if (request.Mode == HoldMode.Session && string.IsNullOrWhiteSpace(request.SessionId))
        {
            return new LockAnswer(LockStatus.Refused, "A session hold needs the session's id.");
        }

        return Change(now =>
        {
            var key = KeyFor(name, request);
            var index = _tickets.FindIndex(ticket => string.Equals(ticket.RequesterKey, key, StringComparison.Ordinal));
            var changed = false;
            string token;

            if (index >= 0)
            {
                var existing = _tickets[index];
                token = existing.Token;
                _tickets[index] = existing with { LastSeenUtc = now, Reason = Trimmed(request.Reason) ?? existing.Reason };
            }
            else
            {
                token = Guid.NewGuid().ToString("N");
                var ticket = new LockTicket(
                    token, name, request.Who.Trim(), request.Purpose.Trim(), request.Mode, key, now, now,
                    Trimmed(request.WorkingDirectory), Trimmed(request.SessionId), Trimmed(request.Reason));

                _tickets.Add(ticket);
                _history.Append(new LockEvent(now, LockEventKind.Requested, name, ticket.Who, ticket.Purpose, Detail: ticket.Reason));
                changed = true;
            }

            changed |= Promote(now);

            return (Answer(token, now), changed);
        });
    }

    /// <summary>Reports a ticket's standing, and counts as its requester still being there.</summary>
    public LockAnswer Check(string token) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index >= 0)
            {
                _tickets[index] = _tickets[index] with { LastSeenUtc = now };
            }

            var promoted = Promote(now);

            return (Answer(token, now), promoted);
        });

    /// <summary>
    /// Takes up a granted process hold for the process that will run the work, so the lock is released
    /// the moment that process exits.
    /// </summary>
    public LockAnswer Attach(string token, int processId) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0)
            {
                return (Answer(token, now), false);
            }

            var ticket = _tickets[index];

            if (!ticket.IsHolder)
            {
                return (new LockAnswer(LockStatus.Refused, "It is not this ticket's turn yet, so there is nothing to take up.", token, ticket.Lock), false);
            }

            if (ticket.Mode != HoldMode.Process)
            {
                return (new LockAnswer(LockStatus.Refused, "A session hold is kept by its session, not by a process.", token, ticket.Lock), false);
            }

            if (_liveness.StartOf(processId) is not { } start)
            {
                return (new LockAnswer(LockStatus.Refused, $"Process {processId} is not running, so it cannot hold the lock.", token, ticket.Lock), false);
            }

            _tickets[index] = ticket with { ProcessId = processId, ProcessStart = start, ReservedUntilUtc = null, LastSeenUtc = now };
            _history.Append(new LockEvent(now, LockEventKind.Attached, ticket.Lock, ticket.Who, ticket.Purpose, Detail: $"process {processId}"));

            return (Answer(token, now), true);
        });

    /// <summary>Lets a lock go, or leaves the queue. Only the token's owner knows the token.</summary>
    public LockAnswer Release(string token) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0)
            {
                return (Answer(token, now), false);
            }

            var ticket = _tickets[index];

            End(index, now, ticket.IsHolder ? LockEventKind.Released : LockEventKind.WaiterDropped, "Released by its holder.",
                ticket.IsHolder ? null : "Left the queue.");
            Promote(now);

            return (new LockAnswer(LockStatus.Ended, $"Released the {ticket.Lock} lock.", token, ticket.Lock), true);
        });

    /// <summary>
    /// Takes a waiting request out of the queue by who asked and what for, the way it was asked, for a
    /// requester that no longer has its token. Never touches a holder: a running holder's lock ends
    /// with its process, and taking it early would let the next run start beside it.
    /// </summary>
    public LockAnswer Leave(LockRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (LockNames.Find(request.Lock) is not { } name)
        {
            return new LockAnswer(LockStatus.Refused, $"There is no lock called '{request.Lock}'.");
        }

        return Change(now =>
        {
            var key = KeyFor(name, request);
            var index = _tickets.FindIndex(ticket => string.Equals(ticket.RequesterKey, key, StringComparison.Ordinal));

            if (index < 0)
            {
                return (new LockAnswer(LockStatus.Unknown, $"Nothing from {request.Who} for {request.Purpose} is waiting for the {name} lock."), false);
            }

            var ticket = _tickets[index];

            if (ticket.IsHolder)
            {
                return (new LockAnswer(LockStatus.Refused,
                    $"{request.Who} holds the {name} lock for {request.Purpose}; it is released when that run exits, not by leaving.",
                    ticket.Token, name), false);
            }

            End(index, now, LockEventKind.WaiterDropped, "Left the queue.", "Left the queue.");
            Promote(now);

            return (new LockAnswer(LockStatus.Ended, $"Left the queue for the {name} lock.", ticket.Token, name), true);
        });
    }

    /// <summary>Lets go of a session's hold on, or place in the queue for, one lock.</summary>
    public LockAnswer ReleaseForSession(string sessionId, string lockName)
    {
        var token = Snapshot()
            .FirstOrDefault(ticket => ticket.Mode == HoldMode.Session
                                      && string.Equals(ticket.SessionId, sessionId, StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(ticket.Lock, LockNames.Find(lockName), StringComparison.Ordinal))?.Token;

        return token is null
            ? new LockAnswer(LockStatus.Unknown, $"This session neither holds nor is waiting for the {lockName} lock.")
            : Release(token);
    }

    /// <summary>
    /// Moves a waiting ticket to a place in its lock's queue, counting from 0 among the waiters. Nigel
    /// only, from the board; a holder cannot be moved.
    /// </summary>
    public bool Reorder(string token, int position) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0 || _tickets[index].IsHolder)
            {
                return (false, false);
            }

            var ticket = _tickets[index];
            _tickets.RemoveAt(index);

            var waiters = Waiters(ticket.Lock).ToList();
            var place = Math.Clamp(position, 0, waiters.Count);
            var insertAt = place < waiters.Count
                ? _tickets.IndexOf(waiters[place])
                : waiters.Count > 0 ? _tickets.IndexOf(waiters[^1]) + 1 : _tickets.Count;

            _tickets.Insert(insertAt, ticket);
            _history.Append(new LockEvent(now, LockEventKind.Reordered, ticket.Lock, ticket.Who, ticket.Purpose, Detail: $"moved to {place + 1} in the queue"));

            return (true, true);
        });

    /// <summary>Takes a waiting ticket out of the queue. Nigel only, from the board.</summary>
    public bool Cancel(string token) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0 || _tickets[index].IsHolder)
            {
                return (false, false);
            }

            End(index, now, LockEventKind.Cancelled, "Cancelled by Nigel from the board.", null);

            return (true, true);
        });

    /// <summary>Takes a lock from its holder. Nigel only, from the board, and never automatic.</summary>
    public bool ForceRelease(string lockName) =>
        Change(now =>
        {
            var index = _tickets.FindIndex(ticket => ticket.IsHolder && string.Equals(ticket.Lock, LockNames.Find(lockName), StringComparison.Ordinal));

            if (index < 0)
            {
                return (false, false);
            }

            End(index, now, LockEventKind.ForceReleased, "Released by Nigel from the board.", null);
            Promote(now);

            return (true, true);
        });

    /// <summary>
    /// Records the test hosts running under each test run, ends holds whose process or session is gone
    /// (clearing what a dead test run left behind), grants nobody took up and waiters that stopped
    /// asking, then gives each free lock to the front of its queue.
    /// </summary>
    public void Sweep() => Change(now =>
    {
        var recorded = RecordTestHosts();

        return (true, Promote(now) | recorded);
    });

    private bool RecordTestHosts()
    {
        if (_tree is null)
        {
            return false;
        }

        var changed = false;

        for (var index = 0; index < _tickets.Count; index++)
        {
            var ticket = _tickets[index];

            if (ticket is not { IsHolder: true, Lock: LockNames.Tests, ProcessId: { } processId })
            {
                continue;
            }

            var known = ticket.TestHosts ?? [];
            var fresh = _tree.TestHostsUnder(processId)
                .Where(host => !known.Any(seen => seen.ProcessId == host.ProcessId && seen.Start == host.Start))
                .ToList();

            if (fresh.Count > 0)
            {
                _tickets[index] = ticket with { TestHosts = [.. known, .. fresh] };
                changed = true;
            }
        }

        return changed;
    }

    private T Change<T>(Func<DateTimeOffset, (T Result, bool Changed)> work)
    {
        T result;
        bool changed;

        lock (_gate)
        {
            var now = _clock.UtcNow;
            var swept = Expire(now);

            (result, changed) = work(now);
            changed |= swept;

            if (changed)
            {
                _store.Save(_tickets);
            }
        }

        if (changed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result;
    }

    private bool Expire(DateTimeOffset now)
    {
        var changed = false;
        var gone = _tickets
            .Where(ticket => ticket.Mode == HoldMode.Session)
            .Select(ticket => ticket.SessionId!)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(sessionId => sessionId, SessionGone, StringComparer.Ordinal);

        for (var index = _tickets.Count - 1; index >= 0; index--)
        {
            var ticket = _tickets[index];

            if (Ending(ticket, now, gone) is { } ending)
            {
                var cleaned = ending.Kind == LockEventKind.HolderGone && ticket is { Lock: LockNames.Tests, ProcessId: not null, GrantedUtc: { } granted }
                    ? _cleaner?.Clean(ticket.TestHosts ?? [], granted, now)
                    : null;

                End(index, now, ending.Kind, ending.Detail, cleaned is null ? ending.Detail : $"{ending.Detail} {cleaned}");
                changed = true;
            }
        }

        return changed;
    }

    private (LockEventKind Kind, string Detail)? Ending(LockTicket ticket, DateTimeOffset now, IReadOnlyDictionary<string, bool> gone)
    {
        if (ticket.Mode == HoldMode.Session)
        {
            return gone[ticket.SessionId!]
                ? (ticket.IsHolder ? LockEventKind.HolderGone : LockEventKind.WaiterDropped, "Its session ended.")
                : null;
        }

        if (!ticket.IsHolder)
        {
            return now - ticket.LastSeenUtc > WaiterGrace
                ? (LockEventKind.WaiterDropped, $"It stopped asking for {Say(now - ticket.LastSeenUtc)}.")
                : null;
        }

        if (ticket.ProcessId is { } processId)
        {
            return _liveness.StartOf(processId) == ticket.ProcessStart
                ? null
                : (LockEventKind.HolderGone, $"Process {processId}, which held it, exited.");
        }

        return ticket.ReservedUntilUtc is { } until && until <= now
            ? (LockEventKind.ReservationExpired, "It was granted but nothing took it up in time.")
            : null;
    }

    private bool SessionGone(string sessionId)
    {
        if (_liveness.IsSessionAlive(sessionId))
        {
            _sessionMisses.Remove(sessionId);

            return false;
        }

        var misses = _sessionMisses.GetValueOrDefault(sessionId) + 1;
        _sessionMisses[sessionId] = misses;

        return misses >= SessionMissesBeforeGone;
    }

    private bool Promote(DateTimeOffset now)
    {
        var changed = false;

        foreach (var name in LockNames.All)
        {
            if (_tickets.Any(ticket => ticket.IsHolder && ticket.Lock == name))
            {
                continue;
            }

            var index = _tickets.FindIndex(ticket => ticket.Lock == name);

            if (index < 0)
            {
                continue;
            }

            var next = _tickets[index];
            _tickets[index] = next with
            {
                GrantedUtc = now,
                ReservedUntilUtc = next.Mode == HoldMode.Process ? now + Reservation : null,
            };
            _history.Append(new LockEvent(now, LockEventKind.Granted, name, next.Who, next.Purpose, WaitedSeconds: Seconds(now - next.RequestedUtc)));
            changed = true;
        }

        return changed;
    }

    private void End(int index, DateTimeOffset now, LockEventKind kind, string clientDetail, string? historyDetail)
    {
        var ticket = _tickets[index];
        _tickets.RemoveAt(index);

        _history.Append(ticket.GrantedUtc is { } granted
            ? new LockEvent(now, kind, ticket.Lock, ticket.Who, ticket.Purpose, Seconds(granted - ticket.RequestedUtc), Seconds(now - granted), historyDetail)
            : new LockEvent(now, kind, ticket.Lock, ticket.Who, ticket.Purpose, Seconds(now - ticket.RequestedUtc), Detail: historyDetail));

        if (ticket.SessionId is { } sessionId && !_tickets.Any(other => other.SessionId == sessionId))
        {
            _sessionMisses.Remove(sessionId);
        }

        _ended[ticket.Token] = clientDetail;
        _endedOrder.Enqueue(ticket.Token);

        while (_endedOrder.Count > EndedRemembered)
        {
            _ended.Remove(_endedOrder.Dequeue());
        }
    }

    private LockAnswer Answer(string token, DateTimeOffset now)
    {
        var index = IndexOf(token);

        if (index < 0)
        {
            return _ended.TryGetValue(token, out var detail)
                ? new LockAnswer(LockStatus.Ended, detail, token)
                : new LockAnswer(LockStatus.Unknown, "This Theseus has no record of that ticket.", token);
        }

        var ticket = _tickets[index];

        if (ticket.IsHolder)
        {
            var detail = ticket.ReservedUntilUtc is { } until
                ? $"You hold the {ticket.Lock} lock. Start the work within {Say(until - now)} or it passes to the next in line."
                : $"You hold the {ticket.Lock} lock.";

            return new LockAnswer(LockStatus.Granted, detail, token, ticket.Lock, 0, null, ticket.ReservedUntilUtc);
        }

        var position = Waiters(ticket.Lock).ToList().IndexOf(ticket) + 1;
        var holder = _tickets.FirstOrDefault(other => other.IsHolder && other.Lock == ticket.Lock);
        var behind = holder is null
            ? "the lock is being handed on"
            : $"held by {holder.Who} for {holder.Purpose}, for {Say(now - holder.GrantedUtc!.Value)}";

        return new LockAnswer(LockStatus.Waiting, $"Waiting for the {ticket.Lock} lock, {Ordinal(position)} in the queue; {behind}.",
            token, ticket.Lock, position, holder);
    }

    private IEnumerable<LockTicket> Waiters(string name) =>
        _tickets.Where(ticket => !ticket.IsHolder && ticket.Lock == name);

    private int IndexOf(string token) =>
        _tickets.FindIndex(ticket => string.Equals(ticket.Token, token, StringComparison.Ordinal));

    private static LockTicket Resumed(LockTicket ticket, DateTimeOffset now)
    {
        if (!ticket.IsHolder)
        {
            return ticket with { LastSeenUtc = now };
        }

        return ticket is { Mode: HoldMode.Process, ProcessId: null }
            ? ticket with { ReservedUntilUtc = now + Reservation }
            : ticket;
    }

    private static string KeyFor(string name, LockRequest request) =>
        request.Mode == HoldMode.Session
            ? $"{name}|session|{request.SessionId!.Trim()}".ToLowerInvariant()
            : $"{name}|{request.Who.Trim()}|{request.Purpose.Trim()}|{request.WorkingDirectory?.Trim()}".ToLowerInvariant();

    private static string? Trimmed(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static double Seconds(TimeSpan span) => Math.Round(Math.Max(0, span.TotalSeconds), 1);

    private static string Say(TimeSpan span)
    {
        if (span < TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(0, (int)span.TotalSeconds)}s";
        }

        return span < TimeSpan.FromHours(1)
            ? $"{(int)span.TotalMinutes} min"
            : $"{(int)span.TotalHours}h{span.Minutes.ToString("00", CultureInfo.InvariantCulture)}";
    }

    private static string Ordinal(int number) => number switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        _ => $"{number}th",
    };
}
