using System.Globalization;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

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

    /// <summary>How far back a later step of a sequence may claim its place from.</summary>
    public static readonly TimeSpan SequenceLimit = TimeSpan.FromHours(12);

    /// <summary>How soon after one step finishes the next must ask to keep the sequence's place.</summary>
    public static readonly TimeSpan StepGap = TimeSpan.FromMinutes(2);

    private const int SessionMissesBeforeGone = 2;
    private const int EndedRemembered = 500;
    private const int DeadlocksRemembered = 50;
    private const int RunsRemembered = 200;

    private readonly Lock _gate = new();
    private readonly List<LockTicket> _tickets;
    private readonly Dictionary<string, string> _ended = new(StringComparer.Ordinal);
    private readonly Queue<string> _endedOrder = new();
    private readonly Dictionary<string, int> _sessionMisses = new(StringComparer.Ordinal);
    private readonly List<StoppedDeadlock> _deadlocks = [];
    private readonly List<RunResult> _runs = [];
    private readonly Func<string, string> _cardFamily;
    private readonly ResourceTableStore? _tableStore;
    private readonly LockStateStore _store;
    private readonly LockHistory _history;
    private readonly ILockLiveness _liveness;
    private readonly ISystemClock _clock;
    private readonly IProcessTree? _tree;
    private readonly IOrphanCleaner? _cleaner;
    private ResourceTable _table;
    private DateTimeOffset _lastAwakeUtc;

    /// <summary>
    /// Opens the book from its saved state. Waiters are given a fresh grace period and unattached grants
    /// a fresh reservation, because nobody could reach Theseus while it was not running.
    /// </summary>
    /// <param name="tree">Finds the test hosts under a test run's holder, so they can be cleared if it dies.</param>
    /// <param name="cleaner">Clears what a dead test run's holder left running before the next is granted.</param>
    /// <param name="table">Each resource's capacity and clashes; the seeded table when not given.</param>
    /// <param name="cardFamily">The card a card's runs count as: a verifier's is the card it verifies.</param>
    /// <param name="tableStore">Where the table and its rulings are kept; read when <paramref name="table"/> is not given.</param>
    public LockBook(
        LockStateStore store,
        LockHistory history,
        ILockLiveness liveness,
        ISystemClock clock,
        IProcessTree? tree = null,
        IOrphanCleaner? cleaner = null,
        ResourceTable? table = null,
        Func<string, string>? cardFamily = null,
        ResourceTableStore? tableStore = null)
    {
        _store = store;
        _history = history;
        _liveness = liveness;
        _clock = clock;
        _tree = tree;
        _cleaner = cleaner;
        _tableStore = tableStore;
        _table = table ?? tableStore?.Load() ?? ResourceTable.Seeded();
        _cardFamily = cardFamily ?? (card => card);

        var now = clock.UtcNow;
        _lastAwakeUtc = now;

        _tickets = store.Load()
            .Select(ticket => WithScope(ticket, _table))
            .Where(ticket => ticket.Claims.Count > 0)
            .Select(ticket => Resumed(ticket, now))
            .ToList();
    }

    /// <summary>The resource table requests are granted under.</summary>
    public ResourceTable Table
    {
        get
        {
            lock (_gate)
            {
                return _table;
            }
        }
    }

    /// <summary>The requests refused because they would have closed a wait cycle, newest last.</summary>
    public IReadOnlyList<StoppedDeadlock> StoppedDeadlocks()
    {
        lock (_gate)
        {
            return [.. _deadlocks];
        }
    }

    /// <summary>Resources asked for that the table does not know, waiting for a ruling; until then they clash with everything.</summary>
    public IReadOnlyList<string> NeedsRuling()
    {
        lock (_gate)
        {
            return [.. _tickets.SelectMany(ticket => ticket.Claims).Select(claim => claim.Resource).Where(resource => !_table.IsKnown(resource)).Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Writes a ruling into the table: from now on a resource has this capacity and clashes with these. A hold
    /// already granted keeps what it was granted.
    /// </summary>
    public void Rule(ResourceRule rule, string ruledBy)
    {
        ArgumentNullException.ThrowIfNull(rule);

        Change(now =>
        {
            _table = _table.With(rule with { RuledBy = ruledBy, RuledUtc = now });
            _tableStore?.Save(_table);
            _history.Append(new LockEvent(now, LockEventKind.Ruled, ResourceClaim.Name(rule.Name), ruledBy, rule.Why,
                Detail: $"capacity {rule.Capacity}; clashes with {(rule.ClashesWith.Count == 0 ? "nothing" : string.Join(", ", rule.ClashesWith))}"));
            Promote(now);

            return (true, true);
        });
    }

    /// <summary>Takes a row out of the table, so the resource clashes with everything again. Nigel only, from the board.</summary>
    public bool Overturn(string resource) =>
        Change(now =>
        {
            var name = ResourceClaim.Name(resource);

            if (!_table.Rules.Any(rule => rule.Name == name))
            {
                return (false, false);
            }

            _table = new ResourceTable(_table.Rules.Where(rule => rule.Name != name));
            _tableStore?.Save(_table);
            _history.Append(new LockEvent(now, LockEventKind.Overturned, name, "Nigel", "overturned from the board"));

            return (true, true);
        });

    /// <summary>How the most recent runs held through the lock ended, newest last.</summary>
    public IReadOnlyList<RunResult> RecentRuns()
    {
        lock (_gate)
        {
            return [.. _runs];
        }
    }

    /// <summary>Records how a run ended against its commit and command, and lets its hold go.</summary>
    public LockAnswer Finish(string token, int exitCode) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0)
            {
                return (Answer(token, now), false);
            }

            var ticket = _tickets[index];
            _runs.Add(new RunResult(ticket.Card, ticket.Commit, ticket.Command, exitCode, now, ticket.Who));

            if (_runs.Count > RunsRemembered)
            {
                _runs.RemoveAt(0);
            }

            End(index, now, ticket.IsHolder ? LockEventKind.Released : LockEventKind.WaiterDropped, $"Finished with exit {exitCode}.", null, exitCode);
            Promote(now);

            return (new LockAnswer(LockStatus.Ended, $"Recorded exit {exitCode} and released the {ticket.Lock} lock.", token, ticket.Lock), true);
        });

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

        var name = LockNames.Find(request.Lock);

        if (!string.IsNullOrWhiteSpace(request.Lock) && name is null)
        {
            return new LockAnswer(LockStatus.Refused, $"There is no lock called '{request.Lock}'. The locks are: {string.Join(", ", LockNames.All)}; any other resource is asked for by scope.");
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
            var scope = ScopeOf(name, request.Scope, request.Command, Trimmed(request.Root) ?? Trimmed(request.WorkingDirectory), Trimmed(request.WorkingDirectory));

            if (scope.Count == 0)
            {
                return (new LockAnswer(LockStatus.Refused, "A lock request needs a lock, a scope or a command."), false);
            }

            var label = name ?? string.Join(" + ", scope);
            var key = KeyFor(label, request);
            var index = _tickets.FindIndex(ticket => string.Equals(ticket.RequesterKey, key, StringComparison.Ordinal));
            var changed = false;
            string token;

            if (index >= 0)
            {
                var existing = _tickets[index];
                token = existing.Token;
                _tickets[index] = existing with { LastSeenUtc = now, Reason = Trimmed(request.Reason) ?? existing.Reason };
            }
            else if (!request.Again && SameRun(request) is { } running)
            {
                var state = running.IsHolder ? "granted" : "waiting";
                var since = (running.GrantedUtc ?? running.RequestedUtc).ToString("HH:mm 'UTC'", CultureInfo.InvariantCulture);

                return (new LockAnswer(LockStatus.Refused,
                    $"{running.Card} is already running this command on {running.Commit} ({state}, since {since}). " +
                    "Wait for it: its result is shown by `ndc-test-lock.sh status`.", Lock: running.Lock), false);
            }
            else
            {
                token = Guid.NewGuid().ToString("N");
                var asked = SequencePlace(request, now);
                var ticket = new LockTicket(
                    token, label, request.Who.Trim(), request.Purpose.Trim(), request.Mode, key, now, now,
                    Trimmed(request.WorkingDirectory), Trimmed(request.SessionId), Trimmed(request.Reason),
                    Scope: scope, Owner: OwnerOf(request),
                    Card: Trimmed(request.Card), Command: Trimmed(request.Command), Commit: Trimmed(request.Commit),
                    HeldAlone: scope.Any(claim => !_table.IsKnown(claim.Resource)));

                var place = _tickets.FindIndex(other => !other.IsHolder && other.RequestedUtc > asked);
                _tickets.Insert(place < 0 ? _tickets.Count : place, ticket);

                if (Deadlock(ticket, now) is { } stopped)
                {
                    _tickets.Remove(ticket);
                    _deadlocks.Add(stopped);

                    if (_deadlocks.Count > DeadlocksRemembered)
                    {
                        _deadlocks.RemoveAt(0);
                    }

                    _history.Append(new LockEvent(now, LockEventKind.DeadlockRefused, label, ticket.Who, ticket.Purpose, Detail: stopped.Detail));

                    return (new LockAnswer(LockStatus.Refused, stopped.Detail), true);
                }

                _history.Append(new LockEvent(now, LockEventKind.Requested, label, ticket.Who, ticket.Purpose, Detail: ticket.Reason,
                    Scope: string.Join(" + ", scope), Command: ticket.Command, Commit: ticket.Commit) { Card = ticket.Card, Session = ticket.SessionId });
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

        var lockName = LockNames.Find(request.Lock);

        if (!string.IsNullOrWhiteSpace(request.Lock) && lockName is null)
        {
            return new LockAnswer(LockStatus.Refused, $"There is no lock called '{request.Lock}'.");
        }

        return Change(now =>
        {
            var name = lockName ?? "scope";
            var index = lockName is null
                ? _tickets.FindIndex(ticket => ticket.Mode == HoldMode.Process && KeyFor(ticket.Lock, request) == ticket.RequesterKey)
                : _tickets.FindIndex(ticket => string.Equals(ticket.RequesterKey, KeyFor(lockName, request), StringComparison.Ordinal));

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

    /// <summary>
    /// Lets go of a session's hold on, or place in the queue for, one lock or scope, named as it was asked for.
    /// With neither named, the session's only hold or wait.
    /// </summary>
    public LockAnswer ReleaseForSession(string sessionId, string? lockName, IReadOnlyList<ResourceClaim>? scope = null)
    {
        var label = LockNames.Find(lockName) ?? (scope is { Count: > 0 } ? string.Join(" + ", ScopeOf(null, scope)) : null);
        var mine = Snapshot()
            .Where(ticket => ticket.Mode == HoldMode.Session && string.Equals(ticket.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var token = label is null
            ? (mine.Count == 1 ? mine[0].Token : null)
            : mine.FirstOrDefault(ticket => string.Equals(ticket.Lock, label, StringComparison.Ordinal))?.Token;

        return token is null
            ? new LockAnswer(LockStatus.Unknown, label is null
                ? $"Say which to release: this session holds or waits for {(mine.Count == 0 ? "nothing" : string.Join("; ", mine.Select(ticket => ticket.Lock)))}."
                : $"This session neither holds nor is waiting for {label}.")
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

            var waiters = Waiters(ticket).Where(other => !ReferenceEquals(other, ticket)).ToList();
            var place = Math.Clamp(position, 0, waiters.Count);
            var insertAt = place < waiters.Count
                ? _tickets.IndexOf(waiters[place])
                : waiters.Count > 0 ? _tickets.IndexOf(waiters[^1]) + 1 : _tickets.Count;

            _tickets.Insert(insertAt, ticket);
            _history.Append(new LockEvent(now, LockEventKind.Reordered, ticket.Lock, ticket.Who, ticket.Purpose, Detail: $"moved to {place + 1} in the queue"));

            return (true, true);
        });

    /// <summary>
    /// Moves a waiting ticket to just before or just after another waiting one, as Nigel's Move up and Move down
    /// on a resource's queue do. Nigel only, from the board; a holder cannot be moved.
    /// </summary>
    public bool MoveNextTo(string token, string anchor, bool after) =>
        Change(now =>
        {
            var index = IndexOf(token);
            var target = IndexOf(anchor);

            if (index < 0 || target < 0 || index == target || _tickets[index].IsHolder || _tickets[target].IsHolder)
            {
                return (false, false);
            }

            var ticket = _tickets[index];
            _tickets.RemoveAt(index);
            var place = IndexOf(anchor) + (after ? 1 : 0);
            _tickets.Insert(place, ticket);
            _history.Append(new LockEvent(now, LockEventKind.Reordered, ticket.Lock, ticket.Who, ticket.Purpose,
                Detail: $"moved {(after ? "behind" : "ahead of")} {_tickets[IndexOf(anchor)].Who}"));

            return (true, true);
        });

    /// <summary>Takes one hold from its holder. Nigel only, from the board, and never automatic.</summary>
    public bool ForceReleaseTicket(string token) =>
        Change(now =>
        {
            var index = IndexOf(token);

            if (index < 0 || !_tickets[index].IsHolder)
            {
                return (false, false);
            }

            End(index, now, LockEventKind.ForceReleased, "Released by Nigel from the board.", null);
            Promote(now);

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

            if (ticket is not { IsHolder: true, ProcessId: { } processId } || !ticket.Needs(ResourceTable.SqlContainers))
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
        var changed = Wake(now);
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
                var cleaned = ending.Kind == LockEventKind.HolderGone && ticket is { ProcessId: not null, GrantedUtc: { } granted } && ticket.Needs(ResourceTable.SqlContainers)
                    ? _cleaner?.Clean(ticket.TestHosts ?? [], granted, now)
                    : null;

                End(index, now, ending.Kind, ending.Detail, cleaned is null ? ending.Detail : $"{ending.Detail} {cleaned}");
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Silence is counted only in time Theseus itself was awake: after a gap longer than the waiter grace between two
    /// of its own looks at the book, the machine was asleep, and so was everyone in the queue.
    /// </summary>
    private bool Wake(DateTimeOffset now)
    {
        var gap = now - _lastAwakeUtc;
        _lastAwakeUtc = now;

        if (gap <= WaiterGrace)
        {
            return false;
        }

        var changed = false;

        for (var index = 0; index < _tickets.Count; index++)
        {
            var ticket = _tickets[index];

            if (!ticket.IsHolder)
            {
                _tickets[index] = ticket with { LastSeenUtc = ticket.LastSeenUtc + gap };
                changed = true;
            }
            else if (ticket.ReservedUntilUtc is { } until)
            {
                _tickets[index] = ticket with { ReservedUntilUtc = until + gap };
                changed = true;
            }
        }

        if (changed)
        {
            TheseusLog.Information("Theseus was not running for {Gap}; every waiter's and unattached grant's clock moved on by it", Say(gap));
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
        var plan = Plan();
        var changed = false;

        for (var index = 0; index < _tickets.Count; index++)
        {
            var ticket = _tickets[index];

            if (plan.Overtaken.TryGetValue(ticket.Token, out var overtaken) && overtaken != ticket.Overtaken)
            {
                ticket = ticket with { Overtaken = overtaken };
                _tickets[index] = ticket;
                changed = true;
            }

            if (!plan.Granted.Contains(ticket.Token))
            {
                continue;
            }

            _tickets[index] = ticket with
            {
                GrantedUtc = now,
                ReservedUntilUtc = ticket.Mode == HoldMode.Process ? now + Reservation : null,
            };
            _history.Append(new LockEvent(now, LockEventKind.Granted, ticket.Lock, ticket.Who, ticket.Purpose, WaitedSeconds: Seconds(now - ticket.RequestedUtc)) { Card = ticket.Card, Session = ticket.SessionId });
            changed = true;
        }

        return changed;
    }

    private GrantPlan Plan() =>
        GrantPlanner.Plan(_tickets, _table, (candidate, reserved) => DeadlockCheck.ReservationIsSafe(_tickets, _table, reserved, candidate));

    private StoppedDeadlock? Deadlock(LockTicket request, DateTimeOffset now)
    {
        var plan = Plan();

        return plan.Granted.Contains(request.Token) ? null : DeadlockCheck.Closes(_tickets, request, _table, plan.Reserved, now);
    }

    private void End(int index, DateTimeOffset now, LockEventKind kind, string clientDetail, string? historyDetail, int? exitCode = null)
    {
        var ticket = _tickets[index];
        _tickets.RemoveAt(index);

        var ended = ticket.GrantedUtc is { } granted
            ? new LockEvent(now, kind, ticket.Lock, ticket.Who, ticket.Purpose, Seconds(granted - ticket.RequestedUtc), Seconds(now - granted), historyDetail)
            : new LockEvent(now, kind, ticket.Lock, ticket.Who, ticket.Purpose, Seconds(now - ticket.RequestedUtc), Detail: historyDetail);

        _history.Append(ended with
        {
            Card = ticket.Card,
            Session = ticket.SessionId,
            Scope = string.Join(" + ", ticket.Claims),
            Command = ticket.Command,
            Commit = ticket.Commit,
            ExitCode = exitCode,
        });

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

            return new LockAnswer(LockStatus.Granted, detail, token, ticket.Lock, 0, null, ticket.ReservedUntilUtc, ticket.Claims);
        }

        var position = Waiters(ticket).TakeWhile(other => !ReferenceEquals(other, ticket)).Count() + 1;
        var holder = _tickets.FirstOrDefault(other => other.IsHolder && Clash(other, ticket));
        var behind = holder is null
            ? "what it needs is held back for an earlier request"
            : $"held by {holder.Who} for {holder.Purpose}, for {Say(now - holder.GrantedUtc!.Value)}";

        return new LockAnswer(LockStatus.Waiting, $"Waiting for the {ticket.Lock} lock, {Ordinal(position)} in the queue; {behind}.",
            token, ticket.Lock, position, holder, Scope: ticket.Claims);
    }

    private IEnumerable<LockTicket> Waiters(LockTicket ticket) =>
        _tickets.Where(other => !other.IsHolder && (ReferenceEquals(other, ticket) || Clash(other, ticket)));

    private bool Clash(LockTicket one, LockTicket other) =>
        !_table.Fits(one.GrantClaims, [other.GrantClaims]);

    /// <summary>
    /// What a request is held for: its predefined scope, what it declared, and the least its command needs; a command
    /// the classes do not recognise, with nothing else declared, is held for everything the old tests lock guarded.
    /// </summary>
    private IReadOnlyList<ResourceClaim> ScopeOf(string? name, IReadOnlyList<ResourceClaim>? declared, string? command = null, string? root = null, string? folder = null)
    {
        var least = string.IsNullOrWhiteSpace(command) ? [] : CommandClasses.Shipped.LeastScope(command, root, folder);
        var scope = ResourceClaim.Normalise([.. PredefinedScopes.Find(name, _table) ?? [], .. declared ?? [], .. least]);

        return scope.Count == 0 && !string.IsNullOrWhiteSpace(command)
            ? PredefinedScopes.Find(LockNames.Tests, _table)!
            : scope;
    }

    /// <summary>
    /// Where a later step of a sequence goes in the queue: at the sequence's first request, but only when the same
    /// requester's previous step has just finished, so no one else can claim a place they never had.
    /// </summary>
    private DateTimeOffset SequencePlace(LockRequest request, DateTimeOffset now)
    {
        if (request.Since is not { } since || since >= now || since < now - SequenceLimit)
        {
            return now;
        }

        var who = request.Who.Trim();
        var stepJustEnded = _runs.Any(run => string.Equals(run.Who, who, StringComparison.OrdinalIgnoreCase)
                                             && run.WhenUtc >= since && now - run.WhenUtc <= StepGap);

        return stepJustEnded ? since : now;
    }

    private LockTicket? SameRun(LockRequest request)
    {
        if (Trimmed(request.Card) is not { } card || Trimmed(request.Commit) is not { } commit || Normalised(request.Command) is not { } command)
        {
            return null;
        }

        var family = _cardFamily(card);

        return _tickets.FirstOrDefault(ticket =>
            ticket.Card is { } other
            && string.Equals(_cardFamily(other), family, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ticket.Commit, commit, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Normalised(ticket.Command), command, StringComparison.Ordinal));
    }

    private static string? Normalised(string? command) =>
        string.IsNullOrWhiteSpace(command)
            ? null
            : string.Join(' ', command.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static string? OwnerOf(LockRequest request) =>
        Trimmed(request.Owner) ?? (request.Mode == HoldMode.Session ? Trimmed(request.SessionId) : null);

    private static LockTicket WithScope(LockTicket ticket, ResourceTable table) =>
        ticket.Scope is { Count: > 0 } scope
            ? ticket with { Scope = ResourceClaim.Normalise(scope) }
            : ticket with { Scope = PredefinedScopes.Find(ticket.Lock, table) ?? [] };

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
