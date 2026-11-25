namespace Theseus.Core.Finding;

/// <summary>
/// Whether a card matches the words typed, and where in a title they were found (items 2 and 3). A word matches any part
/// of any word of any field, ignoring case; several words must all match, each in any field.
/// </summary>
public static class CardSearch
{
    /// <summary>Whether every word is found in some field of the card. No words matches every card.</summary>
    public static bool Matches(IReadOnlyList<string> words, SearchableCard card)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(card);

        var fields = card.Fields.ToList();

        return words.All(word => fields.Any(text => text.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The runs of the text that any of the words covers, in order, overlapping runs joined into one.</summary>
    public static IReadOnlyList<TextSpan> Highlights(string? text, IReadOnlyList<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        if (string.IsNullOrEmpty(text) || words.Count == 0)
        {
            return [];
        }

        var covered = new bool[text.Length];

        foreach (var word in words.Where(word => word.Length > 0))
        {
            for (var at = text.IndexOf(word, StringComparison.OrdinalIgnoreCase); at >= 0;
                 at = at + 1 < text.Length ? text.IndexOf(word, at + 1, StringComparison.OrdinalIgnoreCase) : -1)
            {
                Array.Fill(covered, true, at, word.Length);
            }
        }

        var spans = new List<TextSpan>();
        var index = 0;

        while (index < text.Length)
        {
            if (!covered[index])
            {
                index++;
                continue;
            }

            var start = index;

            while (index < text.Length && covered[index])
            {
                index++;
            }

            spans.Add(new TextSpan(start, index - start));
        }

        return spans;
    }
}
