namespace Theseus.Core.Questions;

/// <summary>
/// Claude Code's question about trusting a folder, said as a prompt the session is stopped at. It comes before the session
/// registers and before any hook runs, so nothing else on disk says the tab waits: a launch still in flight with no session
/// in the registry, into folders Claude Code has no trust record for, is taken to be at that question (1.6, decision (b):
/// whatever still stops a session shows as a wait for the owner).
/// </summary>
public static class TrustPrompt
{
    /// <summary>The prompt, or null when the session is registered, no launch is in flight, or every folder is trusted.</summary>
    /// <param name="sessionId">The session the launch starts.</param>
    /// <param name="isRegistered">Whether the session is in Claude Code's registry, which it enters only past the question.</param>
    /// <param name="launchInFlight">Whether the card's launch has a run that has not ended.</param>
    /// <param name="untrusted">The trust records the launch's folders lack, as Options › Folder trust names them.</param>
    /// <param name="now">When this is said.</param>
    public static PermissionPrompt? For(string sessionId, bool isRegistered, bool launchInFlight, IReadOnlyList<string> untrusted,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(untrusted);

        return !isRegistered && launchInFlight && untrusted.Count > 0
            ? new PermissionPrompt(sessionId, $"Claude needs you to trust {string.Join(", ", untrusted)} before it starts", now)
            : null;
    }
}
