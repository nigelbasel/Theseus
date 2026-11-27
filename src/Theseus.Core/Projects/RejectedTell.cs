namespace Theseus.Core.Projects;

/// <summary>
/// What the card of a rejected spec or rule is told (board-small-changes item 4, 2026-11-26): told as an accepted one is
/// (<see cref="AcceptedTell"/>), so its gate waits again for a new version.
/// </summary>
public static class RejectedTell
{
    /// <summary>The text the card is told.</summary>
    /// <param name="spec">The version rejected.</param>
    /// <param name="note">The owner's note, or null.</param>
    public static string Text(SpecAtGate spec, string? note)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var said = string.IsNullOrWhiteSpace(note) ? "He left no note, so ask him what to change." : $"His note: {note.Trim()}";

        return $"Your {(spec.IsRule ? "rule" : "spec")} (version {spec.ShortVersion}) was rejected: change it as the owner says, "
               + $"then ask him to accept it again. {said}";
    }
}
