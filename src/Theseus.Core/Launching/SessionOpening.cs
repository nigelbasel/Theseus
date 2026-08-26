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

    /// <summary>Whether this workstream has been worked before.</summary>
    public static bool HasHistory(Workstream workstream, bool handoffExists)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        return handoffExists || !string.IsNullOrWhiteSpace(workstream.LastSessionId);
    }

    /// <summary>The prompt a session should open with.</summary>
    public static string For(
        string? preamble,
        string? continuePrompt,
        Workstream workstream,
        bool hasHistory,
        string handoffPath)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        if (!hasHistory)
        {
            return SessionPrompt.Compose(preamble, workstream.Prompt);
        }

        var body = string.IsNullOrWhiteSpace(continuePrompt)
            ? DefaultContinuePrompt
            : continuePrompt.Trim();

        return SessionPrompt.Compose(
            preamble,
            body.Replace(HandoffPlaceholder, handoffPath, StringComparison.Ordinal));
    }
}
