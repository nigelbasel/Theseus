namespace Theseus.Core.Running;

/// <summary>Every headless slot is in use; the run starts when its turn comes.</summary>
/// <param name="Position">Its place in the queue, 1 for next.</param>
public sealed record RunQueued(int Position) : RunRequestOutcome;
