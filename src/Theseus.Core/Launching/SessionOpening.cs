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
        string? planPath = null)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        var identity = WorkstreamIdentity.For(workstream, handoffPath, planPath);

        if (!hasHistory)
        {
            return SessionPrompt.Compose(identity, SessionPrompt.Compose(preamble, workstream.Prompt));
        }

        var body = string.IsNullOrWhiteSpace(continuePrompt)
            ? DefaultContinuePrompt
            : continuePrompt.Trim();

        var continuation = body.Replace(HandoffPlaceholder, handoffPath, StringComparison.Ordinal);

        if (!handoffExists)
        {
            continuation = $"{continuation}{Environment.NewLine}{Environment.NewLine}{MissingHandoffNote}";
        }

        return SessionPrompt.Compose(identity, SessionPrompt.Compose(preamble, continuation));
    }
}
