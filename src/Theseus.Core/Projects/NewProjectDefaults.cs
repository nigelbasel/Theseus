namespace Theseus.Core.Projects;

/// <summary>
/// What New project suggests and grants by default, from the method's branch conventions (<c>method-v0.3</c>): a primary
/// branch named <c>&lt;user&gt;/&lt;project&gt;</c>, and the project's branch rules granted when the box stays ticked
/// and the primary branch is one a grant can cover.
/// </summary>
public static class NewProjectDefaults
{
    /// <summary>The primary branch suggested for a project: <c>&lt;user&gt;/&lt;name&gt;</c>, or the name alone with no usable user.</summary>
    /// <param name="user">The user's sign-in name, made into a branch name's part.</param>
    /// <param name="name">The project's name, a slug's shape, or empty while none is given.</param>
    public static string SuggestedPrimary(string? user, string? name)
    {
        var project = name?.Trim() ?? string.Empty;
        var prefix = ProjectMembership.SlugFrom(user);

        return project.Length == 0 ? string.Empty
            : prefix.Length == 0 ? project
            : $"{prefix}/{project}";
    }

    /// <summary>Whether starting this project grants its branch rules: the box ticked, and a primary branch a grant can cover.</summary>
    public static bool GrantsBranchRules(NewProjectDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        return details.GrantBranchRules && ProjectBranchRules.HasPrimary(details.PrimaryBranch);
    }
}
