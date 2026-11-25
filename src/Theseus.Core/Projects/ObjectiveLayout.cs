namespace Theseus.Core.Projects;

/// <summary>One objective's row of the project view: its slugs, in the file's order.</summary>
/// <param name="Objective">The objective.</param>
/// <param name="Entries">Its slugs.</param>
public sealed record ObjectiveLayout(ProjectObjective Objective, IReadOnlyList<SlugEntry> Entries);
