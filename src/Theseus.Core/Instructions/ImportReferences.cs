using System.Text.RegularExpressions;

namespace Theseus.Core.Instructions;

/// <summary>
/// The <c>@path</c> imports in a CLAUDE.md. Claude Code expands these at launch, skipping anything in
/// a code span or a fenced block, and resolves a relative path against the file that holds it rather
/// than the working directory.
/// </summary>
public static partial class ImportReferences
{
    /// <summary>The paths a file imports, as written.</summary>
    public static IReadOnlyList<string> In(string text)
    {
        var found = new List<string>();
        var fenced = false;

        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("```", StringComparison.Ordinal) || trimmed.StartsWith("~~~", StringComparison.Ordinal))
            {
                fenced = !fenced;

                continue;
            }

            if (fenced)
            {
                continue;
            }

            foreach (Match match in Token().Matches(CodeSpan().Replace(line, " ")))
            {
                found.Add(match.Groups["path"].Value.TrimEnd('.', ':', '!', '?'));
            }
        }

        return found;
    }

    /// <summary>
    /// Where an import points, or null when nothing is there. Prose uses <c>@</c> for other things -
    /// a mention, a decorator - and those resolve to no file, which is how they are told apart.
    /// </summary>
    public static string? Resolve(string reference, string importingFile, string homeDirectory)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        try
        {
            var path = reference.StartsWith("~/", StringComparison.Ordinal) || reference.StartsWith(@"~\", StringComparison.Ordinal)
                ? Path.Combine(homeDirectory, reference[2..])
                : Path.IsPathRooted(reference)
                    ? reference
                    : Path.Combine(Path.GetDirectoryName(importingFile) ?? string.Empty, reference);

            var full = Path.GetFullPath(path);

            return File.Exists(full) ? full : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    [GeneratedRegex(@"(?<![\w@`/\\])@(?<path>(?:~[/\\]|[A-Za-z]:[/\\]|\.{1,2}[/\\]|[\w.-])[^\s`<>""'()\[\],;]*)")]
    private static partial Regex Token();

    [GeneratedRegex("`[^`]*`")]
    private static partial Regex CodeSpan();
}
