using System.Text.RegularExpressions;

namespace Theseus.Core.Worktrees;

/// <summary>
/// The repos a Repos cell names (worktree-per-repo AC 2): split on commas, backticks and anything in parentheses
/// dropped, and anything still holding a space, or saying <c>none</c> or <c>read-only</c>, left out as prose.
/// </summary>
public static partial class RepoEntries
{
    /// <summary>The entries, in order, each once.</summary>
    public static IReadOnlyList<string> Split(string? cell)
    {
        if (string.IsNullOrWhiteSpace(cell))
        {
            return [];
        }

        var plain = Parenthesised().Replace(cell.Replace("`", string.Empty, StringComparison.Ordinal), string.Empty);

        return [.. plain.Split(',')
            .Select(entry => entry.Trim().TrimEnd('/', '\\'))
            .Where(IsRepo)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The last segment of a reference: its repo's folder name.</summary>
    public static string LastSegment(string reference) =>
        reference.Replace('\\', '/').TrimEnd('/').Split('/').Last();

    /// <summary>A reference's segments, for comparing tails.</summary>
    public static IReadOnlyList<string> Segments(string reference) =>
        reference.Replace('\\', '/').Split('/',StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsRepo(string entry) =>
        entry.Length > 0
        && !entry.Any(char.IsWhiteSpace)
        && !entry.StartsWith('[')
        && !entry.StartsWith('<')
        && !entry.Contains('*', StringComparison.Ordinal)
        && !string.Equals(entry, "none", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(entry, "read-only", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\([^)]*\)")]
    private static partial Regex Parenthesised();
}
