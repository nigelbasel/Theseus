namespace Theseus.Core.Sessions;

/// <summary>Tidies a console title into something that can be shown and compared.</summary>
public static class ConsoleTitle
{
    /// <summary>
    /// Strips the leading activity glyph a session animates while it works, so a title is stable
    /// enough to match a terminal tab against and does not flicker when shown.
    /// </summary>
    public static string Clean(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var span = title.AsSpan().Trim();
        var start = 0;

        while (start < span.Length && !char.IsLetterOrDigit(span[start]))
        {
            start++;
        }

        return start >= span.Length ? span.ToString() : span[start..].ToString();
    }

    /// <summary>Whether a console title and a tab title name the same session.</summary>
    public static bool Matches(string? consoleTitle, string? tabTitle)
    {
        var console = Clean(consoleTitle);
        var tab = Clean(tabTitle);

        return console.Length > 0
               && tab.Length > 0
               && string.Equals(console, tab, StringComparison.OrdinalIgnoreCase);
    }
}
