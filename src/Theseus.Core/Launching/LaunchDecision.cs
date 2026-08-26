using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <summary>The outcome of asking whether, and how, a workstream may be started.</summary>
/// <param name="Allowed">Whether the launch may proceed.</param>
/// <param name="Mode">How to start the session when the launch is allowed.</param>
/// <param name="SessionId">
/// The session identifier to pass to Claude Code: an existing one when resuming, a freshly minted
/// one otherwise.
/// </param>
/// <param name="BlockedReason">Why the launch was refused.</param>
/// <param name="LiveSessions">The live sessions that blocked the launch, so the board can focus one.</param>
public sealed record LaunchDecision(
    bool Allowed,
    LaunchMode Mode,
    string SessionId,
    LaunchBlockedReason BlockedReason,
    IReadOnlyList<LiveSession> LiveSessions)
{
    /// <summary>Creates an allowed decision.</summary>
    public static LaunchDecision Allow(LaunchMode mode, string sessionId) =>
        new(true, mode, sessionId, LaunchBlockedReason.None, []);

    /// <summary>Creates a refused decision.</summary>
    public static LaunchDecision Block(LaunchBlockedReason reason, IReadOnlyList<LiveSession> live) =>
        new(false, LaunchMode.FirstRun, string.Empty, reason, live);
}
