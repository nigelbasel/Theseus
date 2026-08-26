using System.Text.RegularExpressions;

namespace Theseus.Core.Domain;

/// <summary>
/// Finds the pull requests a prompt mentions. Sessions refer to them by number in passing — "PR
/// 20769 has merge conflicts" — and that number is the only handle on them anywhere in the plan.
/// </summary>
public static partial class PullRequestExtractor
{
    /// <summary>Extracts every distinct pull request number, in the order they first appear.</summary>
    public static IReadOnlyList<int> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return ReferencePattern().Matches(text)
            .Select(match => int.TryParse(match.Groups["number"].Value, out var number) ? number : 0)
            .Where(number => number > 0)
            .Distinct()
            .ToList();
    }

    [GeneratedRegex(@"\b(?:PRs?|pull request)\s*#?\s*(?<number>\d{1,7})\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();
}
