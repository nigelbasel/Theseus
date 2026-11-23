namespace Theseus.Core.Questions;

/// <summary>
/// A prompt a session is stopped at in its tab, such as Claude Code asking leave to use a tool, as its Notification hook
/// reported it (1.6, Nigel 2026-11-23: sessions run unattended, and whatever still stops one shows as a wait for him).
/// </summary>
/// <param name="SessionId">The session stopped.</param>
/// <param name="Text">What Claude Code said, one line: "Claude needs your permission to use Bash".</param>
/// <param name="AskedUtc">When the hook recorded it.</param>
public sealed record PermissionPrompt(string SessionId, string Text, DateTimeOffset AskedUtc)
{
    /// <summary>
    /// The prompt to show for a session, or null. Approving a prompt fires no hook event, so a recorded prompt is only
    /// believed while the registry says the session waits, and not once the session has asked something since.
    /// </summary>
    /// <param name="prompt">The recorded prompt, if any.</param>
    /// <param name="registryStatus">The session's status in Claude Code's registry: "waiting" while it is stopped.</param>
    /// <param name="question">The session's open question to the owner, if any.</param>
    public static PermissionPrompt? Showing(PermissionPrompt? prompt, string? registryStatus, OwnerQuestion? question) =>
        prompt is not null
        && string.Equals(registryStatus, "waiting", StringComparison.OrdinalIgnoreCase)
        && (question is null || prompt.AskedUtc > question.AskedUtc)
            ? prompt
            : null;
}
