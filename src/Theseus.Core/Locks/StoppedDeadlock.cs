namespace Theseus.Core.Locks;

/// <summary>A request refused because granting it in time was impossible: who was in the cycle, over what.</summary>
public sealed record StoppedDeadlock(
    DateTimeOffset WhenUtc,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> Resources,
    string Detail);
