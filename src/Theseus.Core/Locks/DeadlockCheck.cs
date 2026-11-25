namespace Theseus.Core.Locks;

/// <summary>
/// Finds wait cycles by graph reduction, which is exact for resources with several slots: repeatedly take every owner
/// that is not waiting, release all it holds, and grant every waiter that then fits. A waiter still waiting when
/// nothing more can move would wait for ever. Held-back resources (a waiter overtaken too often) take part: a later
/// waiter that clashes with a held-back one cannot be granted before it.
/// </summary>
public static class DeadlockCheck
{
    /// <summary>The tokens of the waiters that could never be granted.</summary>
    public static IReadOnlySet<string> Stuck(IReadOnlyList<LockTicket> tickets, ResourceTable table, IReadOnlySet<string> reserved)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(reserved);

        List<LockTicket> holds = [.. tickets.Where(ticket => ticket.IsHolder)];
        List<LockTicket> pending = [.. tickets.Where(ticket => !ticket.IsHolder)];
        bool moved = true;

        while (moved && pending.Count > 0)
        {
            HashSet<string> waiting = [.. pending.Select(ticket => ticket.OwnerKey)];
            moved = holds.RemoveAll(hold => !waiting.Contains(hold.OwnerKey)) > 0;

            for (int index = 0; index < pending.Count; index++)
            {
                LockTicket waiter = pending[index];
                bool behindReserved = pending.Take(index).Any(earlier => reserved.Contains(earlier.Token) && !table.Fits(earlier.Claims, [waiter.Claims]));

                if (behindReserved || !table.Fits(waiter.Claims, holds.Select(hold => hold.Claims)))
                {
                    continue;
                }

                holds.Add(waiter);
                pending.RemoveAt(index);
                index--;
                moved = true;
            }
        }

        return pending.Select(ticket => ticket.Token).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether holding back what <paramref name="candidate"/> needs leaves nobody waiting for ever who would not be already.</summary>
    public static bool ReservationIsSafe(IReadOnlyList<LockTicket> tickets, ResourceTable table, IReadOnlySet<string> reserved, LockTicket candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        IReadOnlySet<string> before = Stuck(tickets, table, reserved);
        IReadOnlySet<string> after = Stuck(tickets, table, new HashSet<string>(reserved, StringComparer.Ordinal) { candidate.Token });

        return after.IsSubsetOf(before);
    }

    /// <summary>
    /// The cycle a waiting request would close, written for the requester, or null when it would be granted in time.
    /// </summary>
    public static StoppedDeadlock? Closes(IReadOnlyList<LockTicket> tickets, LockTicket request, ResourceTable table, IReadOnlySet<string> reserved, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(request);

        if (!Stuck(tickets, table, reserved).Contains(request.Token))
        {
            return null;
        }

        List<string> owners = [];
        List<string> resources = [];
        List<string> steps = [];
        LockTicket waiter = request;
        HashSet<string> seen = new(StringComparer.Ordinal);

        while (seen.Add(waiter.OwnerKey))
        {
            List<LockTicket> mine = [.. tickets.Where(ticket => ticket.IsHolder && ticket.OwnerKey == waiter.OwnerKey)];
            LockTicket? blocker = tickets.FirstOrDefault(ticket => ticket.IsHolder && !table.Fits(waiter.Claims, [ticket.Claims]));
            string name = NameOf(tickets, waiter);
            string wanted = Joined(waiter.Claims.Where(claim => blocker is null || !table.Fits([claim], [blocker.Claims])));
            string held = Joined(mine.SelectMany(ticket => ticket.Claims));

            owners.Add(name);
            resources.AddRange(waiter.Claims.Select(claim => claim.Resource));
            steps.Add(steps.Count == 0
                ? $"{name} holds {(held.Length == 0 ? "nothing" : held)} and waits for {wanted}"
                : $"which {name} holds while waiting for {wanted}");

            if (blocker is null || tickets.FirstOrDefault(ticket => !ticket.IsHolder && ticket.OwnerKey == blocker.OwnerKey && ticket.Token != request.Token) is not { } next)
            {
                break;
            }

            waiter = next;
        }

        string detail = $"Granting this would deadlock: {string.Join(", ", steps)}. Release what this session holds and ask again for the whole scope at once.";

        return new StoppedDeadlock(now, [.. owners.Distinct(StringComparer.Ordinal)], [.. resources.Distinct(StringComparer.Ordinal)], detail);
    }

    private static string NameOf(IReadOnlyList<LockTicket> tickets, LockTicket ticket) =>
        tickets.FirstOrDefault(other => other.IsHolder && other.OwnerKey == ticket.OwnerKey)?.Who ?? ticket.Who;

    private static string Joined(IEnumerable<ResourceClaim> claims) =>
        string.Join(" + ", claims.Select(claim => claim.ToString()).Distinct(StringComparer.Ordinal));
}
