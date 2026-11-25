using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// The short-lived card Update starts for a project whose project.md does not read (1.1e AC 19; Nigel, 2026-11-21: "you
/// are the creator of the project files ... it is useless asking me to fix the project"): it works in the project's
/// folder, is told the file, the reading's reason and the method's current shape, changes the file's shape only, records
/// that in its Decisions table, and stops.
/// </summary>
/// <param name="Title">Its title.</param>
/// <param name="WorkingDirectory">The project's folder.</param>
/// <param name="Prompt">Its opening prompt.</param>
public sealed record ProjectUpdaterCard(string Title, string WorkingDirectory, string Prompt)
{
    /// <summary>The role every updater card has.</summary>
    public const WorkstreamRole Role = WorkstreamRole.ProjectUpdater;

    /// <summary>What a project card whose file does not read says on its face (AC 18).</summary>
    public const string NeedsUpdating = "Needs updating";

    /// <summary>What it says while its updater runs.</summary>
    public const string Updating = "Updating…";

    /// <summary>What it says when an updater ran and the file still does not read.</summary>
    public const string StillNeedsUpdating = "Still needs updating";

    /// <summary>
    /// The face's words for an unreadable project (review of PR 4, finding 3): Updating… while its updater runs; Still needs
    /// updating once an updater has run since the file stopped reading, whatever it now says is wrong; otherwise Needs
    /// updating, as when an updater fixed an earlier break weeks ago.
    /// </summary>
    /// <param name="updaterRunning">Whether its updater has a session now.</param>
    /// <param name="ranSinceItBroke">Whether an updater has run since the file stopped reading.</param>
    public static string Face(bool updaterRunning, bool ranSinceItBroke) =>
        updaterRunning ? Updating : ranSinceItBroke ? StillNeedsUpdating : NeedsUpdating;

    /// <summary>The updater for a project.</summary>
    /// <param name="project">The project folder's name.</param>
    /// <param name="folder">Its folder.</param>
    /// <param name="problem">Why the file does not read, as the reading says it, with its line.</param>
    /// <param name="shape">The method's description of project.md: its shape, what is read, and the rules.</param>
    public static ProjectUpdaterCard For(string project, string folder, string problem, string shape)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(problem);
        ArgumentNullException.ThrowIfNull(shape);

        var file = Path.Combine(folder, ProjectCatalog.ProjectFileName);

        var title = $"Update the project file of {project}";

        return new ProjectUpdaterCard(title, folder, string.Join(Environment.NewLine,
            $"Theseus cannot read {file}: {problem.Trim().TrimEnd('.')}.",
            "Bring it into the method's current shape, shown below, so Theseus reads it again. Change its shape only: keep every "
            + "objective, criterion, tick, workstream row, decision and line of prose it has, moved to where the shape puts it. "
            + "Invent nothing; write a bracketed placeholder where the shape needs something the file does not say.",
            $"Record what you changed as one row of its Decisions table, dated today, by \"{title}\".",
            "When it is done, say so in a line and stop: change nothing else, in this folder or any other. Theseus asks you to "
            + "write your handover and exit once the file has changed.",
            string.Empty,
            "The shape, as the method describes it:",
            "```markdown",
            shape.TrimEnd(),
            "```"));
    }
}
