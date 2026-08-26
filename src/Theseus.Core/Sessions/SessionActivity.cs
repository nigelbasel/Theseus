using Theseus.Core.Domain;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads what a live session says it is doing.
///
/// The registry reports this per session and Theseus had been discarding it, showing every live
/// session alike. The distinction is the useful one on a board: a session that is working wants to
/// be left alone, and a session waiting for a reply is the one thing on the board asking for a
/// person. Telling them apart is the difference between a board you scan and a board you read.
/// </summary>
public static class SessionActivity
{
    /// <summary>What a live session's reported status means for its card.</summary>
    public static SessionLifecycle For(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "busy" => SessionLifecycle.Running,
        "waiting" => SessionLifecycle.NeedsAttention,
        "idle" => SessionLifecycle.Idle,
        _ => SessionLifecycle.Running,
    };
}
