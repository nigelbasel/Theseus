namespace Theseus.Core.Locks;

/// <summary>
/// Testcontainers containers no lock holder accounts for (Nigel, 2026-10-06): left over from before the
/// lock, or from a run that went around the guard, with nothing to reap them. One such SQL container
/// cost a workstream a day on 2026-10-02. They are shown for Nigel to remove, never removed
/// automatically, because a leftover cannot be told apart from a run in progress outside the lock.
/// </summary>
public static class UnownedContainers
{
    /// <summary>
    /// The containers not created during the current tests hold. Those belong to the run holding the
    /// lock, which kept every other container-backed run out while it held it.
    /// </summary>
    public static IReadOnlyList<TestContainer> From(IReadOnlyList<TestContainer> containers, IReadOnlyList<LockTicket> tickets)
    {
        var heldSince = tickets.FirstOrDefault(ticket => ticket.IsHolder && ticket.Needs(ResourceTable.SqlContainers))?.GrantedUtc;

        return [.. containers
            .Where(container => heldSince is null || container.CreatedUtc < heldSince)
            .OrderBy(container => container.CreatedUtc)];
    }

    /// <summary>
    /// Who was holding something when an unowned container started: the holds that may have started containers they
    /// did not declare. Since <c>sql-containers</c> is exclusive, at most one hold can own them, so a container started
    /// while no hold had it is on one of these. Shown, never acted on.
    /// </summary>
    public static IReadOnlyList<string> StartedDuring(TestContainer container, IReadOnlyList<LockTicket> tickets)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(tickets);

        return [.. tickets
            .Where(ticket => ticket.IsHolder && ticket.GrantedUtc <= container.CreatedUtc && !ticket.Needs(ResourceTable.SqlContainers))
            .Select(ticket => ticket.Who)
            .Distinct(StringComparer.Ordinal)];
    }
}
