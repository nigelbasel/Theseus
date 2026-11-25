namespace Theseus.Core.Locks;

/// <summary>
/// The one granting rule. The queue is a single list in request order. A request is granted when its whole scope
/// fits beside every hold; it may go ahead of an earlier waiter it clashes with until that waiter has been passed
/// <see cref="OvertakeLimit"/> times, after which what the waiter needs is held back for it as if already held.
/// </summary>
public static class GrantPlanner
{
    public const int OvertakeLimit = 2;

    /// <param name="mayReserve">Whether holding back for a waiter is safe; a reservation that would close a wait
    /// cycle is not made.</param>
    public static GrantPlan Plan(IReadOnlyList<LockTicket> tickets, ResourceTable table, Func<LockTicket, bool>? mayReserve = null)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(table);

        List<IReadOnlyList<ResourceClaim>> taken = [.. tickets.Where(ticket => ticket.IsHolder).Select(ticket => ticket.Claims)];
        List<LockTicket> waiting = [];
        HashSet<string> granted = new(StringComparer.Ordinal);
        HashSet<string> reserved = new(StringComparer.Ordinal);
        Dictionary<string, int> overtaken = new(StringComparer.Ordinal);

        foreach (LockTicket ticket in tickets.Where(ticket => !ticket.IsHolder))
        {
            if (table.Fits(ticket.Claims, taken))
            {
                granted.Add(ticket.Token);
                taken.Add(ticket.Claims);

                foreach (LockTicket passed in waiting.Where(earlier => !table.Fits(earlier.Claims, [ticket.Claims])))
                {
                    overtaken[passed.Token] = Count(passed, overtaken) + 1;
                }

                continue;
            }

            waiting.Add(ticket);

            if (Count(ticket, overtaken) >= OvertakeLimit && (mayReserve?.Invoke(ticket) ?? true))
            {
                reserved.Add(ticket.Token);
                taken.Add(ticket.Claims);
            }
        }

        return new GrantPlan(granted, overtaken, reserved);
    }

    private static int Count(LockTicket ticket, Dictionary<string, int> overtaken) =>
        overtaken.TryGetValue(ticket.Token, out int count) ? count : ticket.Overtaken;
}
