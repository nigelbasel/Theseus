using Theseus.Core.Domain;

namespace Theseus.Core.Mcp;

/// <summary>
/// A start or resume given to a card whose session is already open. An idle session given an instruction takes it as its
/// next turn, recorded as a hand-in; only a session in the middle of something refuses it.
/// </summary>
public static class LiveStart
{
    /// <summary>Whether the start is handed in to the open session rather than launching one.</summary>
    public static bool IsHandIn(bool isLive, SessionLifecycle lifecycle, string? instruction) =>
        isLive && lifecycle == SessionLifecycle.Idle && !string.IsNullOrWhiteSpace(instruction);

    /// <summary>Why a start cannot go to this open session, or null when it is not open or takes the start as a hand-in.</summary>
    public static string? Refusal(string title, bool isLive, SessionLifecycle lifecycle, string? instruction) =>
        !isLive || IsHandIn(isLive, lifecycle, instruction)
            ? null
            : lifecycle switch
            {
                SessionLifecycle.Idle => $"{title} is open and idle: give it an instruction, and it is handed in as its next turn.",
                SessionLifecycle.NeedsAttention => $"{title} is waiting for the user to answer it.",
                SessionLifecycle.Launching => $"{title} is starting.",
                _ => $"{title} is running.",
            };
}
