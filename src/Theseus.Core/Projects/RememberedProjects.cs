using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>readable-projects.json</c>: the names of the projects that read when a catalogue last landed, so a start in the
/// first seconds after launch, before this run's first catalogue, still opens a project's card in the project's window
/// (PR 3 review, item 2). Which cards belong to which project is already in the board file. Kept in Theseus's data
/// folder; an unreadable file reads as none, which opens cards where they would open with no projects.
/// </summary>
public sealed class RememberedProjects
{
    /// <summary>Creates a store over a file.</summary>
    public RememberedProjects(string filePath) => FilePath = filePath;

    /// <summary>Where the names are kept.</summary>
    public string FilePath { get; }

    /// <summary>The names last remembered.</summary>
    public IReadOnlySet<string> Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            return (JsonSerializer.Deserialize<List<string>>(text, TheseusJson.Readable) ?? [])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The remembered projects in {Path} could not be read", FilePath);

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Remembers these names, replacing the last.</summary>
    public void Remember(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        try
        {
            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(names.Order(StringComparer.OrdinalIgnoreCase).ToList(), TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not remember the readable projects in {Path}", FilePath);
        }
    }
}
