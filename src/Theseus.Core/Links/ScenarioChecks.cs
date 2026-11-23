using System.Text.RegularExpressions;

namespace Theseus.Core.Links;

/// <summary>
/// The work a holdout scenario checks, named in its own text by a line "Checks: <slug>, <slug>" (Nigel, 2026-11-23,
/// A2). Theseus reads it to decide whether a build has scenarios of its own, so one folder's scenarios never judge every
/// build in it. Read by Theseus, never by a session.
/// </summary>
public static partial class ScenarioChecks
{
    /// <summary>The names a scenario's Checks lines give, lower case.</summary>
    /// <param name="text">The scenario's text.</param>
    public static IReadOnlySet<string> NamesIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return ChecksLine().Matches(text)
            .SelectMany(match => match.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(name => name.Trim('`', '*', '_', '"', '\'', ' ').ToLowerInvariant())
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // A line that begins, after any list marker or emphasis, with "Checks:".
    [GeneratedRegex(@"^[ \t>*_\-]*checks[*_]*:[*_]*[ \t]*(?<names>[^\r\n]*)\r?$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ChecksLine();
}
