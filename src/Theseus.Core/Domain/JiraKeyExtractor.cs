using System.Text.RegularExpressions;

namespace Theseus.Core.Domain;

/// <summary>
/// Finds the Jira issues a prompt mentions. A workstream routinely names several — an issue plus the
/// pull requests it spans, or two issues being migrated together — so the result is always a list.
/// </summary>
public static partial class JiraKeyExtractor
{
    /// <summary>Extracts every distinct Jira key from a body of text, in the order they first appear.</summary>
    public static IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return KeyPattern().Matches(text)
            .Where(match => !LooksLikeAYear(match.Groups["number"].Value))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool LooksLikeAYear(string number) =>
        number.Length == 4 && number.StartsWith("20", StringComparison.Ordinal);

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]{1,5}-(?<number>\d{1,5})\b")]
    private static partial Regex KeyPattern();
}
