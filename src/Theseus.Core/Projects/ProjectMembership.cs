using System.Globalization;
using System.Text;
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

    /// <summary>
    /// The slug a title suggests: accents dropped, lower-cased, and every run of anything else made one hyphen, so
    /// "Café Menu: v2" is "cafe-menu-v2". Empty when the title has no letter or digit.
    /// </summary>
    public static string SlugFrom(string? title)
    {
        string plain = new([.. (title ?? "").Normalize(NormalizationForm.FormD)
            .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)]);

        return NotSlug().Replace(plain.ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NotSlug();

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugShape();
}
