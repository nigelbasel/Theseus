using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The edits the project view offers on its project's file (1.1c AC 8), each made by the shell through
/// <see cref="Theseus.Core.Projects.ProjectFileWriter"/>: ticks and Rename objective… from a piece's Objective section or
/// an objective heading in Properties (1.1d AC 34, 49), and Add objective… from a phase heading (1.1d AC 49).
/// </summary>
/// <param name="TickCriterion">
/// Ticks (true) or unticks a criterion of an objective, both as the view shows them: the write finds it by the objective's
/// number and its place there and checks its text, since the file may have moved on since the view was drawn (PR 4
/// review, finding 1).
/// </param>
/// <param name="EditObjective">Asks a new title for an objective as the view shows it, and writes it.</param>
/// <param name="AddObjective">Asks for a new objective and adds it at the end of a phase, by its number; null when not offered.</param>
public sealed record ProjectViewEdits(
    Action<ProjectObjective, ProjectCriterion, bool> TickCriterion,
    Action<ProjectObjective> EditObjective,
    Action<int>? AddObjective = null)
{
    /// <summary>
    /// Writes an edit to the project file, saying its second argument in the status bar when done; the actions on waits
    /// use it (project-view-scale, 2026-11-24). Null where the view cannot write.
    /// </summary>
    public Action<Func<string, string>, string>? EditFile { get; init; }

    /// <summary>Asks a line of text: title, label and starting value; null when cancelled.</summary>
    public Func<string, string, string, string?>? AskText { get; init; }
}
