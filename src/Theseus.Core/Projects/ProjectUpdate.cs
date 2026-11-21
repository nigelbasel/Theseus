namespace Theseus.Core.Projects;

/// <summary>
/// Whether Update may start a project's updater now (1.1e AC 19): not while one of the project's own cards runs, since the
/// file is that card's to write, and not while the updater already does.
/// </summary>
public static class ProjectUpdate
{
    /// <summary>Why a project's file does not read, as its card's tooltip and its updater are told.</summary>
    public static string ProblemOf(ProjectFileReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return reading.Status == ProjectReadingStatus.Pinned
            ? $"Pinned to {MethodKits.Pinned}: its project.md is in the older shape"
            : $"Does not read: {reading.Reason}";
    }

    /// <summary>
    /// The folders still said not to be projects: one that reads is a project again and leaves the list (review of PR 4,
    /// finding 7), so it does not vanish unannounced if it breaks later.
    /// </summary>
    public static IReadOnlyList<string> StillHidden(IReadOnlyList<string> hidden, IEnumerable<string> readable)
    {
        ArgumentNullException.ThrowIfNull(hidden);
        var reads = readable.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return [.. hidden.Where(name => !reads.Contains(name))];
    }

    /// <summary>
    /// Whether a card is the project's, for the refusal (review of PR 4, finding 1): its file has named it at some time,
    /// which is remembered after it stops reading, or the card works in the project's folder.
    /// </summary>
    public static bool IsMember(string cardKey, string workingDirectory, IReadOnlySet<string> named, string projectFolder) =>
        named.Contains(cardKey)
        || string.Equals(Path.TrimEndingDirectorySeparator(workingDirectory), Path.TrimEndingDirectorySeparator(projectFolder),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>The file's fingerprint, kept when its updater starts.</summary>
    public static string Fingerprint(string? text) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty)));

    /// <summary>
    /// Whether a running updater is asked to stop (review of PR 4, finding 2, and its re-check): its file has changed since
    /// it started, and its turn has ended, so a conversion in several writes is not cut off after the first.
    /// </summary>
    /// <param name="startedOn">The file's fingerprint when it started; empty once it has been asked.</param>
    /// <param name="reading">The file as read now.</param>
    /// <param name="turnEnded">Whether its session is waiting, its turn over.</param>
    public static bool IsDone(string startedOn, ProjectFileReading reading, bool turnEnded)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return turnEnded && startedOn.Length > 0 && !string.Equals(Fingerprint(reading.Text), startedOn, StringComparison.Ordinal);
    }

    /// <summary>
    /// What is remembered for the next run: the members of what reads, and the last members of a project whose file is
    /// there but does not read, so a restart still knows its cards (review of PR 4, re-check of finding 1).
    /// </summary>
    public static ProjectMembers KeepBroken(ProjectMembers now, ProjectMembers before, IReadOnlyList<ProjectFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(now);
        ArgumentNullException.ThrowIfNull(before);
        var broken = readings.Where(reading => !reading.IsReadable).Select(reading => reading.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var kept = new Dictionary<string, ProjectMember>(now.ByCard, StringComparer.OrdinalIgnoreCase);

        foreach (var (card, member) in before.ByCard.Where(pair => broken.Contains(pair.Value.Project)))
        {
            kept.TryAdd(card, member);
        }

        return ProjectMembers.Remembered(kept);
    }

    /// <summary>Why Update cannot start now, or null when it can.</summary>
    /// <param name="project">The project's folder name.</param>
    /// <param name="cards">Every card on the board: its title, whether it runs, and the project it is a member of.</param>
    /// <param name="updaterRunning">Whether the project's updater runs now.</param>
    public static string? Refusal(string project, IEnumerable<(string Title, bool IsLive, string? Member)> cards, bool updaterRunning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentNullException.ThrowIfNull(cards);

        if (updaterRunning)
        {
            return $"{project}'s project.md is being updated already.";
        }

        var running = cards.Where(card => card.IsLive && string.Equals(card.Member, project, StringComparison.OrdinalIgnoreCase))
            .Select(card => card.Title)
            .ToList();

        return running.Count == 0
            ? null
            : $"{string.Join(", ", running)} of {project} {(running.Count == 1 ? "is" : "are")} running; Update waits until {(running.Count == 1 ? "it has" : "they have")} stopped, since the file is theirs to write.";
    }
}
