namespace Theseus.Core.Projects;

/// <summary>What New project… and Convert to project… start a project with (1.1c AC 6 and 7).</summary>
/// <param name="Name">Its folder's name and identity on the board: a slug's shape.</param>
/// <param name="Title">Its title, the file's first heading; the name when left empty.</param>
/// <param name="Objective">One paragraph: the outcome it exists for; the starter's prompt when left empty.</param>
/// <param name="PrimaryBranch">Its primary branch; the starter's placeholder when left empty.</param>
public sealed record NewProjectDetails(string Name, string Title, string Objective, string PrimaryBranch)
{
    /// <summary>Whether the owner left the box ticked that grants the project's branch rules as it starts.</summary>
    public bool GrantBranchRules { get; init; }
}
