using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// Reads a project file in the <c>method-v0.2</c> shape (method/project.md, spec AC 3). The headings and lines the shape
/// fixes are read strictly; everything else is prose and is left alone. A file that does not follow the shape is
/// unreadable with its line and reason, never read as empty or guessed; a file at <c>method-v0.1</c> is pinned.
/// </summary>
public static partial class ProjectFileReader
{
    private static readonly string[] TableHeader = ["Workstream", "Slug", "Branch", "Repos"];

    private const string TableHeaderText = "| Workstream | Slug | Branch | Repos |";

    /// <summary>Reads a project file's text. <paramref name="name"/> is the file's name without <c>.md</c>.</summary>
    public static ProjectFileReading Read(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);

        try
        {
            return new Parser(name, Lines(text)).Parse();
        }
        catch (RefusalException refusal)
        {
            return ProjectFileReading.Unreadable(name, $"line {refusal.Line}: {refusal.Message}");
        }
    }

    /// <summary>
    /// The <c>Kit:</c> and <c>Primary branch:</c> lines alone, read even when the rest of the file is out of shape, so
    /// the primary branch can be found from the default branch's copy of a file (AC 2).
    /// </summary>
    public static (string? Kit, string? PrimaryBranch) Header(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string? kit = null;
        string? primary = null;

        foreach (var line in Lines(text).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)))
        {
            kit ??= Value(line, "**Kit:**");
            primary ??= Value(line, "**Primary branch:**");
        }

        return (kit, primary);
    }

    private static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    private static string? Value(string line, string label) =>
        line.StartsWith(label, StringComparison.Ordinal) && line[label.Length..].Trim() is { Length: > 0 } value
            ? value.Trim('`')
            : null;

    private static string[] Cells(string line)
    {
        var trimmed = line.Trim();

        if (trimmed.StartsWith('|'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith('|'))
        {
            trimmed = trimmed[..^1];
        }

        return [.. trimmed.Split('|').Select(cell => cell.Trim())];
    }

    [GeneratedRegex(@"^## Phase (\d+): (.+)$")]
    private static partial Regex PhaseHeading();

    [GeneratedRegex(@"^### Objective (\d+)\.(\d+): (.+)$")]
    private static partial Regex ObjectiveHeading();

    [GeneratedRegex(@"^- \[( |x|X)\] (.*)$")]
    private static partial Regex CriterionLine();

    [GeneratedRegex(@"^\|(\s*:?-+:?\s*\|)+$")]
    private static partial Regex SeparatorRow();

    private sealed class RefusalException(int line, string message) : Exception(message)
    {
        public int Line { get; } = line;
    }

    private sealed class Parser(string name, string[] lines)
    {
        private readonly List<ProjectPhase> _phases = [];
        private readonly Dictionary<string, int> _slugLines = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _objectiveNumbers = new(StringComparer.Ordinal);

        private PhaseBuilder? _phase;
        private ObjectiveBuilder? _objective;

        public ProjectFileReading Parse()
        {
            var titleIndex = Array.FindIndex(lines, line => line.Trim().Length > 0);

            if (titleIndex < 0 || !lines[titleIndex].StartsWith("# ", StringComparison.Ordinal))
            {
                throw new RefusalException(titleIndex + 1, "the file must start with a '# <title>' heading");
            }

            var title = lines[titleIndex][2..].Trim();
            var titleLine = titleIndex + 1;
            var (kit, kitLine, primary) = ReadHeader(titleIndex + 1);

            if (kit is null)
            {
                throw new RefusalException(titleLine, "no **Kit:** line under the title");
            }

            if (string.Equals(kit, MethodKits.Pinned, StringComparison.OrdinalIgnoreCase))
            {
                return ProjectFileReading.PinnedTo(name);
            }

            if (!string.Equals(kit, MethodKits.Current, StringComparison.OrdinalIgnoreCase))
            {
                throw new RefusalException(kitLine, $"kit {kit} is not one this Theseus reads (it reads {MethodKits.Current})");
            }

            if (primary is null)
            {
                throw new RefusalException(titleLine, "no **Primary branch:** line under the title");
            }

            ReadBody(titleIndex + 1);

            return ProjectFileReading.Read(new ProjectFile(name, title, MethodKits.Current, primary, _phases));
        }

        private (string? Kit, int KitLine, string? Primary) ReadHeader(int start)
        {
            string? kit = null;
            string? primary = null;
            var kitLine = 0;

            for (var index = start; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
            {
                if (kit is null && Value(lines[index], "**Kit:**") is { } found)
                {
                    kit = found;
                    kitLine = index + 1;
                }

                primary ??= Value(lines[index], "**Primary branch:**");
            }

            return (kit, kitLine, primary);
        }

        private void ReadBody(int start)
        {
            var inFence = false;

            for (var index = start; index < lines.Length; index++)
            {
                var line = lines[index];
                var number = index + 1;

                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    inFence = !inFence;

                    continue;
                }

                if (inFence)
                {
                    continue;
                }

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    ClosePhase();
                    OpenPhase(line, number);

                    continue;
                }

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    CloseObjective();
                    OpenObjective(line, number);

                    continue;
                }

                if (_phase is not null && line.StartsWith("**Exit gate:**", StringComparison.Ordinal))
                {
                    index = ReadExitGate(index);

                    continue;
                }

                if (_objective is not null)
                {
                    index = ReadObjectiveLine(index);

                    continue;
                }

                if (_phase is not null && CriterionLine().IsMatch(line))
                {
                    throw new RefusalException(number, "a criterion outside an objective");
                }
            }

            ClosePhase();
        }

        private void OpenPhase(string line, int number)
        {
            if (!line.StartsWith("## Phase", StringComparison.Ordinal))
            {
                return;
            }

            var match = PhaseHeading().Match(line.TrimEnd());

            if (!match.Success)
            {
                throw new RefusalException(number, "a phase heading is '## Phase <n>: <name>'");
            }

            var phaseNumber = int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

            if (phaseNumber != _phases.Count)
            {
                throw new RefusalException(number, $"phase {phaseNumber} is out of order: phases are numbered from 0, one after another");
            }

            _phase = new PhaseBuilder(phaseNumber, match.Groups[2].Value.Trim(), number);
        }

        private void OpenObjective(string line, int number)
        {
            if (!line.StartsWith("### Objective", StringComparison.Ordinal))
            {
                return;
            }

            var match = ObjectiveHeading().Match(line.TrimEnd());

            if (!match.Success)
            {
                throw new RefusalException(number, "an objective heading is '### Objective <n>.<m>: <title>'");
            }

            var objectiveNumber = $"{match.Groups[1].Value}.{match.Groups[2].Value}";

            if (_phase is null)
            {
                throw new RefusalException(number, $"objective {objectiveNumber} is not under a '## Phase' heading");
            }

            if (match.Groups[1].Value != _phase.Number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            {
                throw new RefusalException(number,
                    $"objective {objectiveNumber} is numbered for phase {match.Groups[1].Value} but sits in phase {_phase.Number}");
            }

            if (!_objectiveNumbers.Add(objectiveNumber))
            {
                throw new RefusalException(number, $"objective {objectiveNumber} appears twice");
            }

            _objective = new ObjectiveBuilder(objectiveNumber, match.Groups[3].Value.Trim(), number);
        }

        private int ReadExitGate(int index)
        {
            var parts = new List<string> { lines[index]["**Exit gate:**".Length..].Trim() };

            while (index + 1 < lines.Length && IsContinuation(lines[index + 1]))
            {
                index++;
                parts.Add(lines[index].Trim());
            }

            _phase!.ExitGates.Add(string.Join(" ", parts.Where(part => part.Length > 0)));

            return index;
        }

        private static bool IsContinuation(string line)
        {
            var trimmed = line.Trim();

            return trimmed.Length > 0
                   && !trimmed.StartsWith('#') && !trimmed.StartsWith('|') && !trimmed.StartsWith("- ", StringComparison.Ordinal)
                   && !trimmed.StartsWith("**", StringComparison.Ordinal) && !trimmed.StartsWith("```", StringComparison.Ordinal);
        }

        private int ReadObjectiveLine(int index)
        {
            var objective = _objective!;
            var line = lines[index];
            var number = index + 1;

            if (CriterionLine().Match(line) is { Success: true } criterion)
            {
                var parts = new List<string> { criterion.Groups[2].Value.Trim() };

                while (index + 1 < lines.Length && lines[index + 1].Length > 0 && char.IsWhiteSpace(lines[index + 1][0])
                       && lines[index + 1].Trim().Length > 0)
                {
                    index++;
                    parts.Add(lines[index].Trim());
                }

                objective.Criteria.Add(new ProjectCriterion(string.Join(" ", parts), criterion.Groups[1].Value != " ", number));

                return index;
            }

            if (line.StartsWith("**Pipeline:**", StringComparison.Ordinal))
            {
                if (objective.PipelineLine > 0)
                {
                    throw new RefusalException(number, $"objective {objective.Number} has a second **Pipeline:** line");
                }

                objective.PipelineLine = number;
                objective.Pipelines.AddRange(Pipelines(line["**Pipeline:**".Length..].Trim(), number));

                return index;
            }

            if (line.TrimStart().StartsWith('|'))
            {
                return ReadTable(index);
            }

            return index;
        }

        private static IEnumerable<PipelineKind> Pipelines(string value, int number)
        {
            if (value == "[to be chosen]")
            {
                return [];
            }

            return value.Split(" or ", StringSplitOptions.TrimEntries).Select(written =>
                Enum.TryParse<PipelineKind>(written, ignoreCase: true, out var kind) && !int.TryParse(written, out _)
                    ? kind
                    : throw new RefusalException(number, $"pipeline '{written}' is not feature, bug or freeform"));
        }

        private int ReadTable(int index)
        {
            var objective = _objective!;
            var number = index + 1;

            if (!Cells(lines[index]).SequenceEqual(TableHeader, StringComparer.Ordinal))
            {
                throw new RefusalException(number, $"the table under objective {objective.Number} is not '{TableHeaderText}'");
            }

            if (objective.TableLine > 0)
            {
                throw new RefusalException(number, $"objective {objective.Number} has a second table");
            }

            if (objective.PipelineLine == 0)
            {
                throw new RefusalException(number, "the workstreams table comes after the **Pipeline:** line");
            }

            objective.TableLine = number;

            if (index + 1 >= lines.Length || !SeparatorRow().IsMatch(lines[index + 1].Trim()))
            {
                throw new RefusalException(number + 1, "the workstreams table's second line must be its separator, '|---|---|---|---|'");
            }

            index++;

            while (index + 1 < lines.Length && lines[index + 1].TrimStart().StartsWith('|'))
            {
                index++;
                objective.Rows.Add(Row(lines[index], index + 1));
            }

            return index;
        }

        private ProjectWorkstreamRow Row(string line, int number)
        {
            var cells = Cells(line);

            if (cells.Length != TableHeader.Length)
            {
                throw new RefusalException(number, $"a workstreams row has {cells.Length} cells, not {TableHeader.Length}");
            }

            for (var cell = 0; cell < cells.Length; cell++)
            {
                if (cells[cell].Length == 0)
                {
                    throw new RefusalException(number, $"a workstreams row has no {TableHeader[cell]}");
                }
            }

            var slug = cells[1].Trim('`');

            if (!ProjectMembership.IsSlug(slug))
            {
                throw new RefusalException(number, $"slug '{slug}' is not lower-case letters, digits and hyphens");
            }

            if (_slugLines.TryGetValue(slug, out var first))
            {
                throw new RefusalException(number, $"slug '{slug}' is listed twice (first at line {first})");
            }

            _slugLines[slug] = number;

            return new ProjectWorkstreamRow(cells[0], slug, cells[2].Trim('`'), cells[3], number);
        }

        private void CloseObjective()
        {
            if (_objective is not { } objective)
            {
                return;
            }

            if (objective.PipelineLine == 0)
            {
                throw new RefusalException(objective.Line, $"objective {objective.Number} has no **Pipeline:** line");
            }

            if (objective.TableLine == 0)
            {
                throw new RefusalException(objective.Line, $"objective {objective.Number} has no workstreams table '{TableHeaderText}'");
            }

            _phase!.Objectives.Add(new ProjectObjective(objective.Number, objective.Title, objective.Criteria,
                objective.Pipelines, objective.Rows, objective.Line));
            _objective = null;
        }

        private void ClosePhase()
        {
            CloseObjective();

            if (_phase is not { } phase)
            {
                return;
            }

            _phases.Add(new ProjectPhase(phase.Number, phase.Name, string.Join(" ", phase.ExitGates), phase.Objectives, phase.Line));
            _phase = null;
        }
    }

    private sealed class PhaseBuilder(int number, string name, int line)
    {
        public int Number { get; } = number;

        public string Name { get; } = name;

        public int Line { get; } = line;

        public List<string> ExitGates { get; } = [];

        public List<ProjectObjective> Objectives { get; } = [];
    }

    private sealed class ObjectiveBuilder(string number, string title, int line)
    {
        public string Number { get; } = number;

        public string Title { get; } = title;

        public int Line { get; } = line;

        public int PipelineLine { get; set; }

        public int TableLine { get; set; }

        public List<ProjectCriterion> Criteria { get; } = [];

        public List<PipelineKind> Pipelines { get; } = [];

        public List<ProjectWorkstreamRow> Rows { get; } = [];
    }
}
