namespace Theseus.Core.Attention;

/// <summary>What <c>attention-open.json</c> holds: the intervals open at the last checkpoint (7.3).</summary>
/// <param name="At">When it was written: the last moment the sampler is known to have run.</param>
/// <param name="Closed">Whether the sampler stopped cleanly then, having written everything it had open.</param>
/// <param name="Open">The intervals open then, each ending at its last sighting.</param>
public sealed record AttentionCheckpointState(DateTimeOffset At, bool Closed, IReadOnlyList<AttentionInterval> Open);
