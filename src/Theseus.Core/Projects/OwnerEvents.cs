using System.Globalization;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>What a session is told of the owner's board actions that concern its card (1.1e AC 27a), and how events() lists them.</summary>
public static class OwnerEvents
{
    /// <summary>A proposal of its approved: the new card's name and key.</summary>
    public static string Approved(string proposalTitle, string cardTitle, string cardKey) =>
        $"The owner approved your proposal \"{proposalTitle}\" on the board: its card is {cardTitle} ({cardKey}).";

    /// <summary>A proposal of its rejected, with the owner's note.</summary>
    public static string Rejected(string proposalTitle, string? note) =>
        $"The owner rejected your proposal \"{proposalTitle}\" on the board{(string.IsNullOrWhiteSpace(note) ? "." : $": {note.Trim()}")}";

    /// <summary>A report to it, or a hand-over to it, confirmed.</summary>
    public static string Confirmed(LinkKind kind, string fromTitle, string? document) =>
        kind == LinkKind.HandsOver
            ? $"The owner accepted {fromTitle}'s hand-over to you on the board{(document is null ? "." : $": {document}")}"
            : $"The owner confirmed {fromTitle}'s report to you on the board{(document is null ? "." : $": {document}")}";

    /// <summary>events()'s answer: each event with its time, oldest first, or that there is nothing new.</summary>
    public static string Describe(IReadOnlyList<OwnerEvent> events, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(zone);

        return events.Count == 0
            ? "Nothing new from the owner on the board since you last asked."
            : string.Join(Environment.NewLine, events.Select(owned =>
                $"{TimeZoneInfo.ConvertTime(owned.AtUtc, zone).ToString("d MMM HH:mm", CultureInfo.InvariantCulture)}: {owned.Text}"));
    }
}
