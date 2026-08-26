namespace Theseus.Core.Sessions;

/// <summary>
/// A session that is running for a workstream the board is no longer following.
/// </summary>
/// <param name="WorkstreamKey">The workstream it belongs to.</param>
/// <param name="SessionId">The session actually running.</param>
/// <param name="SessionName">What that session calls itself.</param>
public sealed record LostSession(string WorkstreamKey, string SessionId, string SessionName);
