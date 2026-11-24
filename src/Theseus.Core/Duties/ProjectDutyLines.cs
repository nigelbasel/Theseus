using System.Text.RegularExpressions;

namespace Theseus.Core.Duties;

/// <summary>
/// The <c>**Duty:**</c> lines a project file carries, one per live duty, under the workstreams table of the objective whose
/// table names the duty's card. They mirror duties.json for people and sessions reading the file: Theseus alone writes
/// them, rewriting every one on each change, and never reads a duty back from them (Nigel, 2026-11-24).
/// </summary>
public static partial class ProjectDutyLines
{
    /// <summary>What every duty line begins with.</summary>
    public const string Prefix = "**Duty:**";

    /// <summary>One duty's line, such as "**Duty:** NDC bug triage · every 20 min · keep the queue running…".</summary>
    public static string Line(Duty duty, string cardTitle)
    {
        ArgumentNullException.ThrowIfNull(duty);

        var paused = duty.IsPaused ? " · paused" : string.Empty;

        return $"{Prefix} {cardTitle} · {duty.Name} · {duty.Cadence.Describe()}{paused} · {duty.FirstLine()}";
    }

    /// <summary>
    /// The file with its duty lines replaced by <paramref name="byObjective"/>: each objective's lines, by its number, go
    /// after the last row of its workstreams table. An objective not listed is left with none.
    /// </summary>
    public static string Write(string text, IReadOnlyDictionary<string, IReadOnlyList<string>> byObjective)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(byObjective);

        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = Without(text.Split(newline));
        var output = new List<string>(lines.Count + byObjective.Sum(each => each.Value.Count + 1));
        string? objective = null;
        var inTable = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];

            if (Heading().Match(line.TrimEnd()) is { Success: true } heading)
            {
                objective = heading.Groups[1].Value;
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                objective = null;
            }

            output.Add(line);

            var isRow = line.TrimStart().StartsWith('|');
            var tableEnds = isRow && (index + 1 >= lines.Count || !lines[index + 1].TrimStart().StartsWith('|'));
            inTable = isRow;

            if (inTable && tableEnds && objective is not null && byObjective.GetValueOrDefault(objective) is { Count: > 0 } duties)
            {
                output.Add(string.Empty);
                output.AddRange(duties);
            }
        }

        return string.Join(newline, output);
    }

    // Every duty line goes, with the blank line Write put between a table and the first of them.
    private static List<string> Without(string[] lines)
    {
        var kept = new List<string>(lines.Length);

        for (var index = 0; index < lines.Length; index++)
        {
            if (lines[index].StartsWith(Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var blankBeforeDuties = lines[index].Length == 0 && index + 1 < lines.Length && lines[index + 1].StartsWith(Prefix, StringComparison.Ordinal)
                                    && kept.Count > 0 && kept[^1].TrimStart().StartsWith('|');

            if (!blankBeforeDuties)
            {
                kept.Add(lines[index]);
            }
        }

        return kept;
    }

    [GeneratedRegex(@"^### Objective (\d+\.\d+): ")]
    private static partial Regex Heading();
}
