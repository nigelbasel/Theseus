namespace Theseus.Core.Questions;

/// <summary>
/// A prompt a session is stopped at in its tab, such as Claude Code asking leave to use a tool, as its Notification hook
/// reported it (1.6, Nigel 2026-11-23: sessions run unattended, and whatever still stops one shows as a wait for him).
/// </summary>
/// <param name="SessionId">The session stopped.</param>
/// <param name="Text">What Claude Code said, one line: "Claude needs your permission to use Bash".</param>
/// <param name="AskedUtc">When the hook recorded it.</param>
public sealed record PermissionPrompt(string SessionId, string Text, DateTimeOffset AskedUtc);
