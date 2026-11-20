namespace Theseus.App.ViewModels;

/// <summary>
/// The edits the project view offers on its project's file (1.1c AC 8), from a piece's Objective section in Properties
/// (1.1d AC 34, Question 3), each made by the shell through <see cref="Theseus.Core.Projects.ProjectFileWriter"/>. A
/// phase's Add objective… is on the project card's menu; the view no longer draws phases (1.1d departure 3).
/// </summary>
/// <param name="TickCriterion">Ticks (true) or unticks a criterion, by its line in the file.</param>
/// <param name="EditObjective">Asks a new title for an objective, by its number, and writes it.</param>
public sealed record ProjectViewEdits(Action<int, bool> TickCriterion, Action<string> EditObjective);
