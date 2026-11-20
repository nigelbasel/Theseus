using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>project-primaries.json</c>: each project's primary branch once found, per listed folder, so later reads go to the
/// primary itself whatever the folder has checked out or the default branch still holds (spec AC 2: "then from the
/// primary itself"). Kept in Theseus's data folder, never in a repository. An unreadable file is logged and treated as
/// empty: the primary is then found again.
/// </summary>
public sealed class ProjectPrimaryStore
{
    private readonly object _gate = new();

    /// <summary>Creates a store over a file.</summary>
    public ProjectPrimaryStore(string filePath) => FilePath = filePath;

    /// <summary>Where the primaries are kept.</summary>
    public string FilePath { get; }

    /// <summary>The primaries remembered for a folder.</summary>
    public IReadOnlyList<RememberedPrimary> For(string folder)
    {
        lock (_gate)
        {
            return [.. Load().Where(entry => SameFolder(entry.Folder, folder))];
        }
    }

    /// <summary>Remembers a project's primary in a folder, replacing what was remembered before.</summary>
    public void Remember(string folder, string project, string branch)
    {
        lock (_gate)
        {
            var entries = Load();

            if (entries.Any(entry => SameFolder(entry.Folder, folder)
                                     && string.Equals(entry.Project, project, StringComparison.OrdinalIgnoreCase)
                                     && string.Equals(entry.Branch, branch, StringComparison.Ordinal)))
            {
                return;
            }

            var kept = entries
                .Where(entry => !(SameFolder(entry.Folder, folder)
                                  && string.Equals(entry.Project, project, StringComparison.OrdinalIgnoreCase)))
                .Append(new RememberedPrimary(folder, project, branch))
                .ToList();

            try
            {
                AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(kept, TheseusJson.Readable));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TheseusLog.Warning(exception, "Could not remember the primary branches in {Path}", FilePath);
            }
        }
    }

    private List<RememberedPrimary> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<RememberedPrimary>>(text, TheseusJson.Readable) ?? [];
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The primary branches in {Path} could not be read, so they are found again", FilePath);

            return [];
        }
    }

    private static bool SameFolder(string one, string other) =>
        string.Equals(one.TrimEnd('\\', '/'), other.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
}
