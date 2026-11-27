namespace Theseus.Core.Attention;

/// <summary>What was true at one instant.</summary>
/// <param name="At">When it was taken.</param>
/// <param name="Attending">The workstream being attended, or null when none was.</param>
/// <param name="Busy">The workstreams whose sessions were working, each once.</param>
public sealed record AttentionSample(
    DateTimeOffset At,
    string? Attending,
    IReadOnlyList<string> Busy)
{
    /// <summary>
    /// Every working session with its card (7.3). When empty, <see cref="Busy"/> stands for one session per card, as
    /// before.
    /// </summary>
    public IReadOnlyList<BusySession> BusySessions { get; init; } = [];

    /// <summary>The session of the card being attended, or null.</summary>
    public string? AttendingSession { get; init; }

    /// <summary>Where the attended card was seen: <c>terminal</c> or <c>theseus</c>.</summary>
    public string? AttendingSource { get; init; }

    /// <summary>What the attended card was part of, or null.</summary>
    public AttentionStamp? AttendingStamp { get; init; }

    /// <summary>The machine's UTC offset at the sample, as <c>+02:00</c>, or null when not known.</summary>
    public string? Offset { get; init; }
}
