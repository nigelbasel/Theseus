namespace Theseus.Core.Projects;

/// <summary>
/// Scenario writers that should not be kept (git-22, 2026-11-23): one whose folder is not its project's holdout folder of
/// today. A writer's folder was fixed in its plan when it was made, so old ones write where nothing is judged.
/// </summary>
public static class WrongWriters
{
    /// <summary>The wrong ones among these.</summary>
    /// <param name="writers">Every scenario-writer card.</param>
    /// <param name="folderOf">A project's holdout folder of today; null for cards in no project.</param>
    public static IReadOnlyList<WrongVerifier> Among(IEnumerable<WriterRecord> writers, Func<string?, string> folderOf)
    {
        ArgumentNullException.ThrowIfNull(writers);
        ArgumentNullException.ThrowIfNull(folderOf);

        return
        [
            .. writers.Where(writer => !writer.IsArchived)
                .Select(writer => (Writer: writer, Folder: folderOf(writer.Project)))
                // A project whose holdout is not found has no folder to judge by (a recorded one with no clone).
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Folder) && !string.IsNullOrWhiteSpace(entry.Writer.Folder)
                                && !Same(entry.Writer.Folder, entry.Folder))
                .Select(entry => new WrongVerifier(entry.Writer.Key, entry.Writer.Title, entry.Writer.Project ?? string.Empty,
                    entry.Writer.IsRunning, $"it writes scenarios in {entry.Writer.Folder}, not this project's holdout {entry.Folder}")),
        ];
    }

    private static bool Same(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
