namespace Theseus.App.ViewModels;

/// <summary>What Add to project… asks for (1.1c AC 7).</summary>
/// <param name="Project">The project, by name.</param>
/// <param name="Objective">The objective it serves, by number.</param>
/// <param name="Slug">The slug of its records folder.</param>
public sealed record AddToProjectChoice(string Project, string Objective, string Slug);
