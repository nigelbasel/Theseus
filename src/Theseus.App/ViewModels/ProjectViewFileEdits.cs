using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The project view's edits on project.md, wired to the questions they ask (1.1c AC 8; 1.1d AC 34, 49): tick or untick a
/// criterion, rename an objective, and add an objective at the end of a phase. The shell's own <c>EditProject</c> writes
/// them; this type holds what each edit asks and what it writes, so a phase heading's Add objective… is tested as the
/// shell wires it.
/// </summary>
public static class ProjectViewFileEdits
{
    /// <summary>The edits for one project.</summary>
    /// <param name="project">The project's name.</param>
    /// <param name="editProject">Writes an edit to the project's file, saying <c>done</c> in the status bar; false when refused.</param>
    /// <param name="fileOf">The project's file as last read, or null.</param>
    /// <param name="askObjective">Asks for a new objective's title and criteria, or null when cancelled.</param>
    /// <param name="askText">Asks for a line of text (title, prompt, current value), or null when cancelled.</param>
    /// <param name="refused">
    /// Draws the view again from the file as last read, after an edit was refused: a criterion or objective that no
    /// longer reads as shown means the view is older than the file (PR 4 review, finding 1).
    /// </param>
    /// <param name="ticked">Told of each criterion the owner ticks or unticks, once written (7.3: piece-events.jsonl), or null.</param>
    public static ProjectViewEdits For(string project, Func<string, Func<string, string>, string, bool> editProject,
        Func<NewObjective?> askObjective, Func<string, string, string, string?> askText, Action refused,
        Action<string, int, bool>? ticked = null)
    {
        ArgumentNullException.ThrowIfNull(editProject);
        ArgumentNullException.ThrowIfNull(askObjective);
        ArgumentNullException.ThrowIfNull(askText);
        ArgumentNullException.ThrowIfNull(refused);

        void Write(Func<string, string> edit, string done)
        {
            if (!editProject(project, edit, done))
            {
                refused();
            }
        }

        return new ProjectViewEdits(
            (objective, criterion, tick) =>
            {
                var index = objective.Criteria.ToList().IndexOf(criterion);

                if (editProject(project, text => ProjectFileEditor.SetCriterion(text, objective.Number, index, criterion.Text, tick),
                        tick ? $"Ticked a criterion of {project}." : $"Unticked a criterion of {project}."))
                {
                    ticked?.Invoke(objective.Number, index, tick);
                }
                else
                {
                    refused();
                }
            },
            objective =>
            {
                if (askText("Rename objective", $"Objective {objective.Number}'s title", objective.Title) is { } title && title != objective.Title)
                {
                    Write(text => ProjectFileEditor.RenameObjective(text, objective.Number, objective.Title, title),
                        $"Objective {objective.Number} of {project} is now {title}.");
                }
            },
            phase =>
            {
                if (askObjective() is { } objective)
                {
                    Write(text => ProjectFileEditor.AddObjective(text, phase, objective.Title, objective.Criteria).Text,
                        $"Added objective {objective.Title} to phase {phase} of {project}.");
                }
            })
        {
            EditFile = Write,
            AskText = askText,
        };
    }
}
