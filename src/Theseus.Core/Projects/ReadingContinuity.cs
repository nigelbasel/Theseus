namespace Theseus.Core.Projects;

/// <summary>
/// Carries a project through one failed read (PR 2 review, item 9): a project that read last time and fails now keeps
/// its last good reading, marked with the failure, and is taken as unreadable only when it fails twice running. A
/// folder no longer listed drops its projects at once. This softens AC 13 by one read, so one transient git failure
/// does not close the view and move a project's cards back to the grid.
/// </summary>
public static class ReadingContinuity
{
    /// <summary>The readings to show, from the last ones shown and the ones just read.</summary>
    /// <param name="previous">What was shown.</param>
    /// <param name="fresh">What was just read.</param>
    /// <param name="failures">Each project's consecutive failed reads, by folder and name; updated here.</param>
    public static IReadOnlyList<ProjectFileReading> Merge(IReadOnlyList<ProjectFileReading> previous,
        IReadOnlyList<ProjectFileReading> fresh, IDictionary<string, int> failures)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(failures);

        var result = new List<ProjectFileReading>();
        var freshByKey = fresh.ToDictionary(Key, StringComparer.OrdinalIgnoreCase);
        var previousByKey = previous.Where(reading => reading.IsReadable)
            .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var reading in fresh)
        {
            var key = Key(reading);

            if (reading.IsReadable)
            {
                failures.Remove(key);

                // Until its slugs are read again it shows the artefacts it had, rather than none.
                result.Add(previousByKey.TryGetValue(key, out var before) && reading.Artefacts.Count == 0
                    ? reading with { Artefacts = before.Artefacts }
                    : reading);
            }
            else if (!previousByKey.ContainsKey(key))
            {
                result.Add(reading);
            }
        }

        foreach (var (key, before) in previousByKey)
        {
            if (freshByKey.TryGetValue(key, out var now) && now.IsReadable)
            {
                continue;
            }

            var folderFailed = fresh.Any(reading => !reading.IsReadable
                                                    && string.Equals(reading.Folder, before.Folder, StringComparison.OrdinalIgnoreCase));

            if (!folderFailed)
            {
                failures.Remove(key);

                continue;
            }

            var count = failures.TryGetValue(key, out var seen) ? seen + 1 : 1;
            failures[key] = count;

            if (count == 1)
            {
                var failure = now?.Reason
                              ?? fresh.First(reading => !reading.IsReadable
                                                        && string.Equals(reading.Folder, before.Folder, StringComparison.OrdinalIgnoreCase)).Reason;

                result.RemoveAll(reading => string.Equals(Key(reading), key, StringComparison.OrdinalIgnoreCase));
                result.Add(before with { LastFailure = failure });
            }
            else
            {
                failures.Remove(key);

                if (now is not null && !result.Contains(now))
                {
                    result.Add(now);
                }
            }
        }

        return result;
    }

    /// <summary>Puts the slugs' artefacts, read after the catalogue, on the readings shown.</summary>
    public static IReadOnlyList<ProjectFileReading> WithArtefacts(IReadOnlyList<ProjectFileReading> shown,
        IReadOnlyList<ProjectFileReading> attached)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(attached);

        var byKey = attached.Where(reading => reading.IsReadable)
            .GroupBy(Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return [.. shown.Select(reading => reading.IsReadable && reading.LastFailure is null
                                           && byKey.TryGetValue(Key(reading), out var read) && read.File == reading.File
            ? reading with { Artefacts = read.Artefacts }
            : reading)];
    }

    private static string Key(ProjectFileReading reading) => $"{reading.Folder}|{reading.Name}";
}
