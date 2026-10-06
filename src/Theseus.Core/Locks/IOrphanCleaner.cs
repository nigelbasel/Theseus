namespace Theseus.Core.Locks;

/// <summary>Removes what a dead lock holder left running, and only that.</summary>
public interface IOrphanCleaner
{
    /// <summary>
    /// Ends the recorded test hosts that are still the same processes, and removes Testcontainers
    /// containers created between <paramref name="fromUtc"/> and <paramref name="toUtc"/>. Returns what
    /// was done, in words for the history, or null when there was nothing to do.
    /// </summary>
    string? Clean(IReadOnlyList<HeldProcess> testHosts, DateTimeOffset fromUtc, DateTimeOffset toUtc);
}
