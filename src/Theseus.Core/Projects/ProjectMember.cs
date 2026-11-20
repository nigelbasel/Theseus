namespace Theseus.Core.Projects;

/// <summary>Where a card is in the projects, as its project file says (1.1c AC 1 and 2).</summary>
/// <param name="Project">The project whose table names the card.</param>
/// <param name="Slug">Its current slug: the last row naming it.</param>
public sealed record ProjectMember(string Project, string Slug);
