using Theseus.Core.Diagnostics;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads every slug's artefacts for a readable project, so the project view's stages need no git on the UI thread: it
/// runs inside the background read (project-view PR 2), and the UI thread only combines them with the owner's
/// acceptances.
/// </summary>
public static class ProjectArtefacts
{
    /// <summary>The reading with its slugs' artefacts; an unreadable reading is returned as it is.</summary>
    /// <param name="reading">The project as read.</param>
    /// <param name="git">Runs git.</param>
    /// <param name="cache">Keeps each slug's last artefacts and re-reads only what moved; null reads everything.</param>
    public static ProjectFileReading Attach(ProjectFileReading reading, IGitReader git, ArtefactCache? cache = null)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(git);

        if (reading.File is not { } file || reading.Folder is not { } folder)
        {
            return reading;
        }

        var repository = new GitRepositoryReader(git, folder);

        if (cache is not null)
        {
            try
            {
                return reading with { Artefacts = cache.Read(repository, file) };
            }
            catch (Exception exception)
            {
                TheseusLog.Error(exception, "Reading the artefacts of {Project} failed", file.Name);

                return reading;
            }
        }

        var kitMoveParent = WorkstreamArtefactReader.KitMoveParent(repository, file);
        var artefacts = new Dictionary<string, WorkstreamArtefacts>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in file.Workstreams)
        {
            try
            {
                artefacts[row.Slug] = WorkstreamArtefactReader.Read(repository, file, row, kitMoveParent);
            }
            catch (Exception exception)
            {
                // One slug's failure leaves that slug unread, never the project or the board.
                TheseusLog.Error(exception, "Reading the artefacts of {Slug} in {Project} failed", row.Slug, file.Name);
                artefacts[row.Slug] = WorkstreamArtefacts.NotOnMachine(row.Slug);
            }
        }

        return reading with { Artefacts = artefacts };
    }
}
