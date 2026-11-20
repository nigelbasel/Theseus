using Theseus.Core.Persistence;

namespace Theseus.Core.Projects;

/// <summary>
/// Writes an owner's edit to a project's <c>project.md</c>, only when the file still holds the text Theseus last read,
/// so an edit never overwrites what a session wrote meanwhile (1.1c AC 9).
/// </summary>
public static class ProjectFileWriter
{
    /// <summary>Applies an edit to the project's file and returns the new text.</summary>
    /// <param name="reading">The project as last read, with its folder and text.</param>
    /// <param name="edit">The edit, from <see cref="ProjectFileEditor"/>.</param>
    public static string Apply(ProjectFileReading reading, Func<string, string> edit)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(edit);

        if (reading.Folder is not { } home || reading.Text is not { } read)
        {
            throw new ProjectEditRefusedException($"{reading.Name} was not read from a project folder");
        }

        var path = Path.Combine(home, ProjectCatalog.ProjectFileName);
        var now = File.ReadAllText(path);

        if (!string.Equals(now, read, StringComparison.Ordinal))
        {
            throw new ProjectEditRefusedException($"{path} changed since Theseus read it; try again once the board has caught up");
        }

        var edited = edit(now);

        // ReadAllText drops a byte order mark and the atomic write adds none; a file that had one keeps it.
        var bytes = File.ReadAllBytes(path);
        var hadMark = bytes is [0xEF, 0xBB, 0xBF, ..];
        AtomicFile.WriteAllText(path, hadMark ? "﻿" + edited : edited);

        return edited;
    }
}
