namespace Theseus.Core.Markdown;

/// <summary>The spec window's search (1.1e AC 24a, Ctrl+F): case-insensitive, from a point, round to the start.</summary>
public static class TextFind
{
    /// <summary>Where the next match starts at or after a point, round to the start; -1 when there is none.</summary>
    public static int Next(string text, string query, int from)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrEmpty(query))
        {
            return -1;
        }

        var start = Math.Clamp(from, 0, text.Length);
        var found = text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);

        return found >= 0 ? found : text.IndexOf(query, 0, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>How many times it is found.</summary>
    public static int Count(string text, string query)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }

        var count = 0;

        for (var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase); at >= 0;
             at = text.IndexOf(query, at + query.Length, StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }
}
