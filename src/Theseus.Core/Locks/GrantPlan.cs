namespace Theseus.Core.Locks;

/// <summary>
/// What one pass over the queue decided: whom to grant now, the new overtake count of each waiter that was
/// passed, and which waiters have what they need held back for them.
/// </summary>
public sealed record GrantPlan(
    IReadOnlySet<string> Granted,
    IReadOnlyDictionary<string, int> Overtaken,
    IReadOnlySet<string> Reserved);
