namespace Theseus.Core.Running;

/// <summary>The run was not started or queued.</summary>
/// <param name="Reason">Why.</param>
/// <param name="Detail">The sentence shown to the owner, naming the limit or breaker and the figures.</param>
public sealed record RunRefused(RefusalReason Reason, string Detail) : RunRequestOutcome;
