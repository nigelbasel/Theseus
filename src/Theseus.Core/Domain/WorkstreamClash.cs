namespace Theseus.Core.Domain;

/// <summary>Something two or more workstreams share that they probably should not.</summary>
/// <param name="Kind">What is shared.</param>
/// <param name="What">The thing itself: a path, or an issue key.</param>
/// <param name="Workstreams">The keys of the workstreams sharing it, in board order.</param>
public sealed record WorkstreamClash(ClashKind Kind, string What, IReadOnlyList<string> Workstreams);
