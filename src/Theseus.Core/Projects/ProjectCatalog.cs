using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads the projects in the projects folder set in Settings, as plain files (1.1b AC 1 and 2): every subfolder that
/// holds a <c>project.md</c> is a project, named by the folder, with its slugs' records read from its <c>work\</c>
/// folder. A folder or file that cannot be read is listed as unreadable with its reason, never left out.
/// </summary>
public static class ProjectCatalog
{
    /// <summary>The file that makes a folder a project home.</summary>
    public const string ProjectFileName = "project.md";

    /// <summary>Reads every project in a projects folder; none when no folder is set (AC 13).</summary>
    public static IReadOnlyList<ProjectFileReading> Read(string? projectsFolder)
    {
        var folder = ProjectsFolderPath.Normalize(projectsFolder);

        if (folder.Length == 0)
        {
            return [];
        }

        try
        {
            if (!Directory.Exists(folder))
            {
                return [Unreadable(folder, "the projects folder does not exist")];
            }

            return [.. Directory.EnumerateDirectories(folder)
                .Where(home => File.Exists(Path.Combine(home, ProjectFileName)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(ReadProjectSafely)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [Unreadable(folder, $"the projects folder could not be read: {exception.Message}")];
        }
    }

    // Reading runs whenever the folder changes; nothing it meets in one project may take the board down with it.
    private static ProjectFileReading ReadProjectSafely(string home)
    {
        try
        {
            return ReadProject(home);
        }
        catch (Exception exception)
        {
            TheseusLog.Error(exception, "Reading the project in {Folder} failed", home);

            return Unreadable(home, $"could not be read: {exception.Message}");
        }
    }

    private static ProjectFileReading ReadProject(string home)
    {
        var name = FolderName(home);
        var text = File.ReadAllText(Path.Combine(home, ProjectFileName));
        var reading = ProjectFileReader.Read(name, text) with { Folder = home, Text = text };

        if (reading.File is not { } file)
        {
            return reading;
        }

        var artefacts = new Dictionary<string, WorkstreamArtefacts>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in file.Workstreams)
        {
            try
            {
                artefacts[row.Slug] = WorkstreamArtefactReader.Read(home, file, row.Slug);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // One slug's failure leaves that slug unread, never the project or the board.
                TheseusLog.Error(exception, "Reading the records of {Slug} in {Project} failed", row.Slug, name);
                artefacts[row.Slug] = WorkstreamArtefacts.NotStarted(row.Slug);
            }
        }

        return reading with { Artefacts = artefacts };
    }

    private static ProjectFileReading Unreadable(string folder, string reason) =>
        ProjectFileReading.Unreadable(FolderName(folder), reason) with { Folder = folder };

    private static string FolderName(string folder) => Path.GetFileName(folder.TrimEnd('\\', '/'));
}
