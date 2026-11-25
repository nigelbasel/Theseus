using System.Text.Json;
using Theseus.Core.Diagnostics;
using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// <c>project-members.json</c>: which card was in which project when the projects were last read, so a start in the
/// first moment after launch, before this run's first read lands, still opens a project's card in the project's window
/// (1.1 PR 3 review, item 2). Membership is read from the project files (1.1c); this is only the last reading of them.
/// Kept in Theseus's data folder; an unreadable file reads as no members, which opens cards where they would open with
/// no projects.
/// </summary>
public sealed class RememberedProjects
{
    /// <summary>Creates a store over a file.</summary>
    public RememberedProjects(string filePath) => FilePath = filePath;

    /// <summary>Where the members are kept.</summary>
    public string FilePath { get; }

    /// <summary>The members last remembered.</summary>
    public ProjectMembers Load()
    {
        var text = AtomicFile.TryReadAllText(FilePath);

        if (string.IsNullOrWhiteSpace(text))
        {
            return ProjectMembers.None;
        }

        try
        {
            return ProjectMembers.Remembered(
                JsonSerializer.Deserialize<Dictionary<string, ProjectMember>>(text, TheseusJson.Readable) ?? []);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning(exception, "The remembered project members in {Path} could not be read", FilePath);

            return ProjectMembers.None;
        }
    }

    /// <summary>Remembers these members, replacing the last.</summary>
    public void Remember(ProjectMembers members)
    {
        ArgumentNullException.ThrowIfNull(members);

        try
        {
            var ordered = members.ByCard
                .OrderBy(member => member.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(member => member.Key, member => member.Value);

            AtomicFile.WriteAllText(FilePath, JsonSerializer.Serialize(ordered, TheseusJson.Readable));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception, "Could not remember the project members in {Path}", FilePath);
        }
    }
}
