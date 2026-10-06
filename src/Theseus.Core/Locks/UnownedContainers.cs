namespace Theseus.Core.Locks;

/// <summary>
/// Testcontainers containers no lock holder accounts for (Nigel, 2026-10-06): left over from before the
/// lock, or from a run that went around the guard, with nothing to reap them. One such SQL container
/// cost the nexus-admin workstream a day on 2026-10-02. They are shown for Nigel to remove, never removed
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
        var heldSince = tickets.FirstOrDefault(ticket => ticket is { IsHolder: true, Lock: LockNames.Tests })?.GrantedUtc;

        return [.. containers
            .Where(container => heldSince is null || container.CreatedUtc < heldSince)
            .OrderBy(container => container.CreatedUtc)];
    }
}
