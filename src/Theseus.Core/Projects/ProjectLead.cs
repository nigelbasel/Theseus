namespace Theseus.Core.Projects;

/// <summary>
/// A project's lead card: its main card on the board, whose session coordinates the project (Nigel, 2026-11-22: "the
/// notion of a lead card for a project ... the main project card on the board, which has a session associated with
/// it"). Chosen in Project properties and kept by project folder name; by default the card of the file's first row.
/// </summary>
public static class ProjectLead
{
    /// <summary>The lead's card key: the one chosen, while a row still names it, else the first row's; null with no rows.</summary>
    public static string? KeyOf(ProjectFile file, IReadOnlyDictionary<string, string> chosen)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(chosen);

        var cards = file.Workstreams.Select(row => row.Workstream).ToList();

        return chosen.TryGetValue(file.Name, out var lead) && cards.Contains(lead, StringComparer.OrdinalIgnoreCase)
            ? lead
            : cards.FirstOrDefault();
    }
}
