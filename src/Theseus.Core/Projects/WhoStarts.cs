namespace Theseus.Core.Projects;

/// <summary>
/// Who starts a card of a running project, and why it is not running yet: F31's "who acts next, and when" for a box whose
/// project decides what runs (project-view-usability item 5, Nigel 2026-11-23). Where this says nothing, the owner is the
/// one to start the card: a stopped project, an ad hoc card, a verifier, or the project's lead.
/// </summary>
public static class WhoStarts
{
    /// <summary>The label of the secondary button that starts such a card by hand, after the same question as before.</summary>
    public const string StartAnywayLabel = "Start anyway…";

    /// <summary>The who-acts line, or null when the owner starts it.</summary>
    /// <param name="project">The running project's title, or null when the card is in no running project.</param>
    /// <param name="lead">The lead card's title, or null when the project has none.</param>
    /// <param name="isLeadOrVerifier">Whether the card is the project's lead or a verifier, which only the owner starts.</param>
    /// <param name="waitsOnInput">Whether it still waits on a hand-over that is not ready.</param>
    public static string? Line(string? project, string? lead, bool isLeadOrVerifier, bool waitsOnInput)
    {
        if (project is null || isLeadOrVerifier)
        {
            return null;
        }

        var who = lead is null ? $"{project}'s sessions start it" : $"{lead} starts it";
        var why = waitsOnInput ? "it waits on a hand-over that is not ready yet" : "its turn has not come yet";

        return $"{who}: {project} is running and decides what runs. Not running because {why}.";
    }
}
