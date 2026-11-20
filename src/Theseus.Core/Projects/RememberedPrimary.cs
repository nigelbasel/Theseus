namespace Theseus.Core.Projects;

/// <summary>A project's primary branch as last found in one listed folder.</summary>
/// <param name="Folder">The listed folder.</param>
/// <param name="Project">The project's name.</param>
/// <param name="Branch">Its primary branch.</param>
public sealed record RememberedPrimary(string Folder, string Project, string Branch);
