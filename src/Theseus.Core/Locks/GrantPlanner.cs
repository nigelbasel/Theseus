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
    public static GrantPlan Plan(IReadOnlyList<LockTicket> tickets, ResourceTable table, Func<LockTicket, IReadOnlySet<string>, bool>? mayReserve = null)
    {
        ArgumentNullException.ThrowIfNull(tickets);
        ArgumentNullException.ThrowIfNull(table);

        List<IReadOnlyList<ResourceClaim>> taken = [.. tickets.Where(ticket => ticket.IsHolder).Select(ticket => ticket.GrantClaims)];
        List<LockTicket> waiting = [];
        HashSet<string> granted = new(StringComparer.Ordinal);
        HashSet<string> reserved = new(StringComparer.Ordinal);
        Dictionary<string, int> overtaken = new(StringComparer.Ordinal);

        void Reserve(LockTicket ticket)
        {
            if (!reserved.Contains(ticket.Token) && (mayReserve?.Invoke(ticket, reserved) ?? true))
            {
                reserved.Add(ticket.Token);
                taken.Add(ticket.GrantClaims);
            }
        }

        foreach (LockTicket ticket in tickets.Where(ticket => !ticket.IsHolder))
        {
            List<LockTicket> passed = [.. waiting.Where(earlier => !table.Fits(earlier.GrantClaims, [ticket.GrantClaims]))];

            // A waiter passed as often as the limit allows gets what it needs held back before anyone passes it again,
            // even when the limit is reached part way through this pass.
            foreach (LockTicket limited in passed.Where(earlier => Count(earlier, overtaken) >= OvertakeLimit))
            {
                Reserve(limited);
            }

            if (table.Fits(ticket.GrantClaims, taken))
            {
                granted.Add(ticket.Token);
                taken.Add(ticket.GrantClaims);

                foreach (LockTicket earlier in passed)
                {
                    overtaken[earlier.Token] = Count(earlier, overtaken) + 1;
                }

                continue;
            }

            waiting.Add(ticket);

            if (Count(ticket, overtaken) >= OvertakeLimit)
            {
                Reserve(ticket);
            }
        }

        return new GrantPlan(granted, overtaken, reserved);
    }

    private static int Count(LockTicket ticket, Dictionary<string, int> overtaken) =>
        overtaken.TryGetValue(ticket.Token, out int count) ? count : ticket.Overtaken;
}
