using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// The shape of a slug, which also names projects and records folders. Which card is in which project is read from the
/// project files (<see cref="ProjectMembers"/>, 1.1c), not decided here.
/// </summary>
public static partial class ProjectMembership
{
    /// <summary>Whether text has a slug's shape: lower-case letters, digits and hyphens.</summary>
    public static bool IsSlug(string? text) => text is not null && SlugShape().IsMatch(text);

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugShape();
}
