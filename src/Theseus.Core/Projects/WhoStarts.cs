namespace Theseus.Core.Projects;

/// <summary>
/// Who starts a card of a running project, and when: F31's "who acts next, and when" for a box whose project decides
/// what runs (project-view-usability item 5, Nigel 2026-11-23). In a running project every member's Start is the owner's
/// override, Start anyway…; a plain ▶ Start is left for a stopped project, an ad hoc card and a verifier.
/// </summary>
public static class WhoStarts
{
    /// <summary>The label of the secondary button that starts such a card by hand, after the same question as before.</summary>
    public const string StartAnywayLabel = "Start anyway…";

    /// <summary>
    /// The who-acts line, or null where the project does not start it: in no running project, or held by the owner (done,
    /// paused, blocked or stopped by him), when the box keeps its own answer.
    /// </summary>
    /// <param name="project">The running project's title, or null when the card is in no running project.</param>
    /// <param name="liveLead">The lead card's title while it has a session, or null.</param>
    /// <param name="heldByOwner">Whether it is done, paused, blocked or stopped by the owner, which the project does not undo.</param>
    /// <param name="waitsOn">The cards whose hand-over it still waits for, by title; empty when none.</param>
    public static string? Line(string? project, string? liveLead, bool heldByOwner, IReadOnlyList<string> waitsOn)
    {
        ArgumentNullException.ThrowIfNull(waitsOn);

        if (project is null || heldByOwner)
        {
            return null;
        }

        var who = liveLead is null ? $"{project}'s sessions start it" : $"{liveLead} starts it";
        var when = waitsOn.Count switch
        {
            0 => "when the project's plan reaches it",
            1 => $"when {waitsOn[0]} hands work over to it",
            _ => $"when {string.Join(" and ", waitsOn)} hand work over to it",
        };

        return $"{who}, {when}: {project} is running and decides what runs.";
    }
}
