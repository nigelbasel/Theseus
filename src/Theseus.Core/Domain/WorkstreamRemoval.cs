namespace Theseus.Core.Domain;

/// <summary>
/// Whether a workstream may be put away or thrown away, and why not when it may not.
///
/// Archiving hides a workstream; deleting destroys its plan file, which is the only copy of the
/// prompt, the working directory and everything else a session was told. Neither is something to do
/// to a workstream whose session is running: the tab would go on working for a card that no longer
/// exists, and the run it is in the middle of would have nowhere to be recorded.
/// </summary>
public static class WorkstreamRemoval
{
    /// <summary>Why this workstream cannot be archived, or null when it can.</summary>
    public static string? RefusalToArchive(bool isLive) => isLive
        ? "This workstream has a session running. Stop it first, then archive."
        : null;

    /// <summary>Why this workstream cannot be deleted, or null when it can.</summary>
    public static string? RefusalToDelete(bool isLive) => isLive
        ? "This workstream has a session running. Stop it first, then delete."
        : null;

    /// <summary>What deleting will actually destroy, said plainly before it is done.</summary>
    public static string Consequence(string key) =>
        $"Delete {key} for good? Its plan file goes, with the prompt and everything the board knows"
        + " about it. Its handover notes and its work are left alone. Archiving hides it instead,"
        + " and can be undone.";
}
