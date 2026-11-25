namespace Theseus.Core.Finding;

/// <summary>
/// What a tag may be (item 9): short lower-case words or phrases of letters, digits and hyphens, such as "security" or
/// "waiting-on-dpo". Typed spaces and underscores become hyphens, so "waiting on dpo" is the same tag.
/// </summary>
public static class TagNames
{
    /// <summary>The longest a tag may be: a label under a card's title, not a sentence.</summary>
    public const int MaximumLength = 32;

    /// <summary>
    /// The tag as stored: lower case, spaces and underscores as hyphens, repeated or edge hyphens removed; or null when
    /// nothing valid is left, it is too long, or a character other than a letter, digit, hyphen, space or underscore was
    /// typed.
    /// </summary>
    public static string? Normalise(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var characters = new List<char>();

        foreach (var ch in typed.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                characters.Add(ch);
            }
            else if (ch is '-' or ' ' or '_')
            {
                if (characters.Count > 0 && characters[^1] != '-')
                {
                    characters.Add('-');
                }
            }
            else
            {
                return null;
            }
        }

        var tag = new string([.. characters]).Trim('-');

        return tag.Length is 0 or > MaximumLength ? null : tag;
    }

    /// <summary>Why a typed tag is refused, for the field that took it; null when it is a valid tag.</summary>
    public static string? WhyNot(string? typed) =>
        string.IsNullOrWhiteSpace(typed) ? "Type a tag first"
        : Normalise(typed) is not null ? null
        : typed.Trim().Length > MaximumLength ? $"A tag is at most {MaximumLength} characters"
        : "A tag is letters, digits and hyphens";

    /// <summary>The tags given, normalised, invalid ones and repeats dropped, in the order first given.</summary>
    public static IReadOnlyList<string> Clean(IEnumerable<string>? tags) =>
        tags is null ? [] : [.. tags.Select(Normalise).OfType<string>().Distinct(StringComparer.Ordinal)];
}
