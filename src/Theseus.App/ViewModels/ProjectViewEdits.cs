namespace Theseus.App.ViewModels;

/// <summary>
/// The edits the project view offers on its project's file (1.1c AC 8), each made by the shell through
/// <see cref="Theseus.Core.Projects.ProjectFileWriter"/>.
/// </summary>
/// <param name="TickCriterion">Ticks (true) or unticks a criterion, by its line in the file.</param>
/// <param name="EditObjective">Asks a new title for an objective, by its number, and writes it.</param>
/// <param name="AddObjective">Asks for a new objective in a phase, by its number, and writes it.</param>
/// <param name="OpenFile">Opens the project's file in its editor.</param>
public sealed record ProjectViewEdits(Action<int, bool> TickCriterion, Action<string> EditObjective, Action<int> AddObjective, Action OpenFile);
