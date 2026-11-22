using System.Text.RegularExpressions;

namespace Theseus.App.ViewModels;

/// <summary>
/// Splits a sentence of the project view into runs, making a link of each objective number ("Objective 1.5") and W-code
/// ("W4") that leads somewhere in the view (I42). A token that leads nowhere stays plain text: a link must not lie.
/// </summary>
public static partial class LinkedSpans
{
    /// <summary>The runs of a text, with links where <paramref name="resolve"/> finds a target.</summary>
    public static IReadOnlyList<LinkedSpan> Of(string? text, Func<string, (Action Follow, string ToolTip)?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var spans = new List<LinkedSpan>();
        var at = 0;

        foreach (Match match in Token().Matches(text))
        {
            if (resolve(match.Value) is not { } target)
            {
                continue;
            }

            if (match.Index > at)
            {
                spans.Add(new LinkedSpan(text[at..match.Index], null, null));
            }

            spans.Add(new LinkedSpan(match.Value, target.Follow, target.ToolTip));
            at = match.Index + match.Length;
        }

        if (at < text.Length)
        {
            spans.Add(new LinkedSpan(text[at..], null, null));
        }

        return spans;
    }

    [GeneratedRegex(@"\b(?:[Oo]bjective \d+(?:\.\d+)*|W\d+)\b")]
    private static partial Regex Token();
}
