namespace Theseus.Core.Finding;

/// <summary>
/// What is typed in the board's search box, split into words and tags: "#bym search" is the tag "bym" and the word
/// "search" (card-search-and-tags items 2 and 12, Nigel 2026-11-21).
/// </summary>
/// <param name="Text">The text as typed, trimmed.</param>
/// <param name="Words">The words, each of which must match some field of a card, in lower case.</param>
/// <param name="Tags">The tags named with <c>#</c>, the same as ticking them in the Tags list.</param>
public sealed record SearchQuery(string Text, IReadOnlyList<string> Words, IReadOnlyList<string> Tags)
{
    /// <summary>Nothing typed.</summary>
    public static SearchQuery Empty { get; } = new(string.Empty, [], []);

    /// <summary>Whether it asks for nothing, so every card shows.</summary>
    public bool IsEmpty => Words.Count == 0 && Tags.Count == 0;

    /// <summary>
    /// Splits text on white space. A word starting with <c>#</c> is a tag, kept only when it is a valid one; a lone
    /// <c>#</c> or an invalid tag is dropped rather than read as a word, so typing "#" does not dim the board to nothing.
    /// </summary>
    public static SearchQuery Parse(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var words = new List<string>();
        var tags = new List<string>();

        foreach (var part in trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith('#'))
            {
                if (TagNames.Normalise(part[1..]) is { } tag && !tags.Contains(tag, StringComparer.Ordinal))
                {
                    tags.Add(tag);
                }
            }
            else
            {
                var word = part.ToLowerInvariant();

                if (!words.Contains(word, StringComparer.Ordinal))
                {
                    words.Add(word);
                }
            }
        }

        return new SearchQuery(trimmed, words, tags);
    }
}
