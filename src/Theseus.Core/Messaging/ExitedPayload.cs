namespace Theseus.Core.Messaging;

/// <summary>Shim to app: Claude Code has exited.</summary>
/// <param name="SessionId">The identifier the session finally ran under.</param>
/// <param name="ExitCode">The exit code Claude Code returned.</param>
/// <param name="DurationMs">How long the session ran, in milliseconds.</param>
public sealed record ExitedPayload(string SessionId, int ExitCode, long DurationMs);
