namespace Theseus.Core.Attention;

/// <summary>What was true at one instant.</summary>
/// <param name="At">When it was taken.</param>
/// <param name="Attending">The workstream being attended, or null when none was.</param>
/// <param name="Busy">The workstreams whose sessions were working, each once.</param>
public sealed record AttentionSample(
    DateTimeOffset At,
    string? Attending,
    IReadOnlyList<string> Busy);
