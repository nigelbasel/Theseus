using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>
/// Chooses what a session is told when it opens.
///
/// A workstream that has run before must not be handed its original opening prompt again: that
/// prompt describes starting the work, and replaying it invites the session to redo what is already
/// done. It is given a continuation instead, pointing at the handoff.
///
/// Whether a workstream has run before is judged from the board and the handoff on disk, not from
/// whether a transcript survived. A session can lose its transcript and still have left notes, and
/// that is exactly the case where being told to start again is most expensive.
/// </summary>
public static class SessionOpening
{
    /// <summary>The placeholder replaced with the workstream's handoff path.</summary>
    public const string HandoffPlaceholder = "{handoff}";

    /// <summary>What a returning session is told when nothing else is configured.</summary>
    public const string DefaultContinuePrompt =
        "Continue the work already under way on this workstream. Read " + HandoffPlaceholder
        + " and any other handover notes first, and pick up from what was left unfinished. "
        + "Do not start again or repeat work that is already done.";

    /// <summary>What a resumed session is told, before the moment it was resumed at.</summary>
    public const string ResumedPrefix = "Resumed by Theseus at ";

    /// <summary>
    /// The prompt a resumed session opens with: one line saying when, and then only what this one
    /// workstream asks of every session.
    ///
    /// A resume keeps the conversation, so the identity, the board's standing prompt and the
    /// continuation are all things the session already knows. Sent again they read as a restart —
    /// and Claude Code queues the opening prompt behind any turn it takes on its own when it
    /// resumes, so the whole opening could land half an hour into the resumed work, telling a busy
    /// session to go back and read its notes. One short line is harmless whenever it arrives.
    /// </summary>
    /// <param name="related">What changed in its links since its last session, or nothing.</param>
    public static string ForResume(Workstream workstream, DateTimeOffset resumedAtUtc, string handoffPath,
        string? related = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var local = resumedAtUtc.ToLocalTime();
        var line = $"{ResumedPrefix}{local:HH:mm} on {local:yyyy-MM-dd}. Your context is intact; carry on where you left off.";
        var own = workstream.EveryStartPrompt?.Replace(HandoffPlaceholder, handoffPath, StringComparison.Ordinal);

        return SessionPrompt.Compose(line, SessionPrompt.Compose(related, own));
    }

    /// <summary>Whether this workstream has been worked before.</summary>
    public static bool HasHistory(Workstream workstream, bool handoffExists)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        return handoffExists || !string.IsNullOrWhiteSpace(workstream.LastSessionId);
    }

    /// <summary>Said when the handoff a continuation points at is not on disk.</summary>
    public const string MissingHandoffNote =
        "That file does not exist, so no handover notes were left. Work out where things stand from "
        + "the working directory and the recent commits before changing anything.";

    /// <summary>The prompt a session should open with.</summary>
    /// <param name="handoffExists">
    /// Whether the handoff is actually on disk. A continuation that names a file which is not there
    /// sends the session looking for something that was never written, so it is told so plainly.
    /// </param>
    public static string For(
        string? preamble,
        string? continuePrompt,
        Workstream workstream,
        bool hasHistory,
        string handoffPath,
        bool handoffExists,
        string? planPath = null,
        string? identityTemplate = null,
        string? related = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var identity = WorkstreamIdentity.For(workstream, handoffPath, planPath, identityTemplate);

        if (!hasHistory)
        {
            return Compose(identity, preamble, workstream.Prompt, related, workstream.EveryStartPrompt, handoffPath);
        }

        var body = string.IsNullOrWhiteSpace(continuePrompt)
            ? DefaultContinuePrompt
            : continuePrompt.Trim();

        var continuation = body.Replace(HandoffPlaceholder, handoffPath, StringComparison.Ordinal);

        if (!handoffExists)
        {
            continuation = $"{continuation}{Environment.NewLine}{Environment.NewLine}{MissingHandoffNote}";
        }

        return Compose(identity, preamble, continuation, related, workstream.EveryStartPrompt, handoffPath);
    }

    /// <summary>
    /// Identity, then the board's standing prompt, then what this start is for, then the workstreams
    /// linked to this one, then what this one workstream asks of every session. Its own instructions
    /// come last because they are the most specific: nearer the work, and the thing a session should
    /// read as having the final word.
    /// </summary>
    private static string Compose(string identity, string? preamble, string? body, string? related,
        string? everyStart, string handoffPath)
    {
        var own = everyStart?.Replace(HandoffPlaceholder, handoffPath, StringComparison.Ordinal);

        return SessionPrompt.Compose(identity, SessionPrompt.Compose(preamble,
            SessionPrompt.Compose(body, SessionPrompt.Compose(related, own))));
    }
}
