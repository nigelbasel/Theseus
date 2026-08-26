namespace Theseus.Core.Messaging;

/// <summary>Shim to app: Claude Code is running.</summary>
/// <param name="SessionId">The session identifier it was started under.</param>
/// <param name="ClaudeProcessId">The process id of the Claude Code process.</param>
public sealed record StartedPayload(string SessionId, int ClaudeProcessId);
