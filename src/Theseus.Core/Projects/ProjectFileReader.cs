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

    private static readonly string[] TableHeaderWithWhat = ["Workstream", "Slug", "Branch", "Repos", "What"];

    private static readonly string[] RoundsHeader = ["Round", "Name", "Began", "Why", "By"];

    private const string TableHeaderText = "| Workstream | Slug | Branch | Repos |";

    private const string RoundsHeaderText = "| Round | Name | Began | Why | By |";

    /// <summary>Reads a project file's text. <paramref name="name"/> is the project home folder's name.</summary>
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

    [GeneratedRegex(@"^[ \t]*- \[( |x|X)\] (.*)$")]
    private static partial Regex CriterionLine();

    [GeneratedRegex(@"^\|(\s*:?-+:?\s*\|)+$")]
    private static partial Regex SeparatorRow();

    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2})\b")]
    private static partial Regex WrittenDate();

    private static DateOnly? EarliestDate(string[] lines) =>
        lines.SelectMany(line => WrittenDate().Matches(line).Select(match => Date(match.Value)))
            .OfType<DateOnly>()
            .Order()
            .Cast<DateOnly?>()
            .FirstOrDefault();

    [GeneratedRegex(@"^\*\*Round:\*\*(.*)$")]
    private static partial Regex RoundLine();

    private static DateOnly? Date(string text) =>
        DateOnly.TryParseExact(text.Trim().Trim('`'), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;

    private sealed class RefusalException(int line, string message) : Exception(message)
    {
        public int Line { get; } = line;
    }

    private sealed class Parser(string name, string[] lines)
    {
        private readonly List<ProjectPhase> _phases = [];
        private readonly Dictionary<string, int> _slugLines = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _objectiveNumbers = new(StringComparer.Ordinal);
        private readonly List<ProjectRound> _rounds = [];
        private readonly List<(string Objective, int Round, int Line)> _roundLines = [];
        private int _roundsLine;
        private DateOnly? _firstDecision;

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
            var (kit, kitLine, primary, beforeKit) = ReadHeader(titleIndex + 1);

            if (kit is null)
            {
                // method-v0.1 had no fixed shape, and its files say so in prose: "Kit: Theseus method-v0.1 (link)".
                if (IsPlainV01KitLine(titleIndex + 1))
                {
                    return ProjectFileReading.PinnedTo(name);
                }

                throw new RefusalException(titleLine, "no **Kit:** line under the title");
            }

            if (string.Equals(kit, MethodKits.Pinned, StringComparison.OrdinalIgnoreCase))
            {
                return ProjectFileReading.PinnedTo(name);
            }

            var readKit = MethodKits.Readable(kit)
                          ?? throw new RefusalException(kitLine, $"kit {kit} is not one this Theseus reads (it reads {string.Join(" and ", MethodKits.Read)})");

            if (primary is null)
            {
                throw new RefusalException(titleLine, "no **Primary branch:** line under the title");
            }

            ReadBody(titleIndex + 1);
            CheckRoundLines();

            return ProjectFileReading.Read(new ProjectFile(name, title, readKit, primary, _phases)
            {
                BeforeKit = beforeKit,
                RoundsTable = _rounds,
                FirstDecision = _firstDecision,
                EarliestDate = EarliestDate(lines),
            });
        }

        private bool IsPlainV01KitLine(int start) =>
            lines.Skip(start)
                .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
                .Any(line => line.TrimStart().StartsWith("Kit:", StringComparison.Ordinal)
                             && line.Contains(MethodKits.Pinned, StringComparison.OrdinalIgnoreCase));

        private (string? Kit, int KitLine, string? Primary, IReadOnlySet<string> BeforeKit) ReadHeader(int start)
        {
            string? kit = null;
            string? primary = null;
            var kitLine = 0;
            var beforeKit = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var index = start; index < lines.Length && !lines[index].StartsWith("## ", StringComparison.Ordinal); index++)
            {
                if (kit is null && Value(lines[index], "**Kit:**") is { } found)
                {
                    kit = found;
                    kitLine = index + 1;
                }

                primary ??= Value(lines[index], "**Primary branch:**");

                if (Value(lines[index], "**Before method-v0.2:**") is { } slugs)
                {
                    beforeKit.UnionWith(slugs.Split(',').Select(slug => slug.Trim().Trim('`')).Where(slug => slug.Length > 0));
                }
            }

            return (kit, kitLine, primary, beforeKit);
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

                    if (line.TrimEnd() == "## Rounds")
                    {
                        index = ReadRounds(index);

                        continue;
                    }

                    if (line.TrimEnd() == "## Decisions")
                    {
                        ReadFirstDecision(index);

                        continue;
                    }

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
                CriterionWaiting? waiting = null;
                string? evidence = null;

                // An indented tick box is a criterion of its own, not more of this one: joined in, an unticked
                // sub-criterion would count as met.
                while (index + 1 < lines.Length && lines[index + 1].Length > 0 && char.IsWhiteSpace(lines[index + 1][0])
                       && lines[index + 1].Trim().Length > 0 && !CriterionLine().IsMatch(lines[index + 1]))
                {
                    index++;
                    var continuation = lines[index].Trim();

                    if (CriterionWaiting.Parse(continuation) is { } note)
                    {
                        waiting = note;
                    }
                    else if (continuation.StartsWith(ProjectCriterion.EvidenceLabel, StringComparison.Ordinal))
                    {
                        evidence = continuation[ProjectCriterion.EvidenceLabel.Length..].Trim();
                    }
                    else
                    {
                        parts.Add(continuation);
                    }
                }

                objective.Criteria.Add(new ProjectCriterion(string.Join(" ", parts), criterion.Groups[1].Value != " ", number)
                {
                    Waiting = waiting,
                    Evidence = evidence,
                });

                return index;
            }

            if (RoundLine().Match(line.TrimEnd()) is { Success: true } round)
            {
                if (objective.RoundLine > 0)
                {
                    throw new RefusalException(number, $"objective {objective.Number} has a second **Round:** line");
                }

                if (objective.Criteria.Count > 0 || objective.PipelineLine > 0)
                {
                    throw new RefusalException(number, $"objective {objective.Number}'s **Round:** line goes directly under its heading");
                }

                if (!int.TryParse(round.Groups[1].Value.Trim(), System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var roundNumber) || roundNumber < 1)
                {
                    throw new RefusalException(number, $"objective {objective.Number}'s **Round:** line is not a round number");
                }

                objective.RoundLine = number;
                objective.Round = roundNumber;
                _roundLines.Add((objective.Number, roundNumber, number));

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

            return value.Split(" or ", StringSplitOptions.TrimEntries).Select(written => written.ToLowerInvariant() switch
            {
                "feature" => PipelineKind.Feature,
                "bug" => PipelineKind.Bug,
                "freeform" => PipelineKind.Freeform,
                _ => throw new RefusalException(number, $"pipeline '{written}' is not feature, bug or freeform"),
            });
        }

        private int ReadTable(int index)
        {
            var objective = _objective!;
            var number = index + 1;

            var header = Cells(lines[index]);

            if (!header.SequenceEqual(TableHeader, StringComparer.Ordinal) && !header.SequenceEqual(TableHeaderWithWhat, StringComparer.Ordinal))
            {
                throw new RefusalException(number,
                    $"the table under objective {objective.Number} is not '{TableHeaderText}', with an optional What column after Repos");
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
                objective.Rows.Add(Row(lines[index], index + 1, header.Length));
            }

            return index;
        }

        private ProjectWorkstreamRow Row(string line, int number, int columns)
        {
            var cells = Cells(line);

            if (cells.Length != columns)
            {
                throw new RefusalException(number, $"a workstreams row has {cells.Length} cells, not {columns}");
            }

            // What may be left empty by a session that has not written it yet; the four before it may not.
            for (var cell = 0; cell < TableHeader.Length; cell++)
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

            return new ProjectWorkstreamRow(cells[0], slug, cells[2].Trim('`'), cells[3], number)
            {
                What = columns > TableHeader.Length && cells[4].Length > 0 ? cells[4] : null,
            };
        }

        private int ReadRounds(int index)
        {
            if (_roundsLine > 0)
            {
                throw new RefusalException(index + 1, $"a second ## Rounds section (the first is at line {_roundsLine})");
            }

            _roundsLine = index + 1;

            while (index + 1 < lines.Length && lines[index + 1].Trim().Length == 0)
            {
                index++;
            }

            if (index + 1 >= lines.Length || !Cells(lines[index + 1]).SequenceEqual(RoundsHeader, StringComparer.Ordinal))
            {
                throw new RefusalException(index + 2, $"the Rounds table is not '{RoundsHeaderText}'");
            }

            index++;

            if (index + 1 >= lines.Length || !SeparatorRow().IsMatch(lines[index + 1].Trim()))
            {
                throw new RefusalException(index + 2, "the Rounds table's second line must be its separator, '|---|---|---|---|---|'");
            }

            index++;

            while (index + 1 < lines.Length && lines[index + 1].TrimStart().StartsWith('|'))
            {
                index++;
                _rounds.Add(RoundRow(lines[index], index + 1));
            }

            if (_rounds.Count == 0)
            {
                throw new RefusalException(_roundsLine, "the Rounds table has no rows");
            }

            return index;
        }

        private ProjectRound RoundRow(string line, int number)
        {
            var cells = Cells(line);

            if (cells.Length != RoundsHeader.Length)
            {
                throw new RefusalException(number, $"a Rounds row has {cells.Length} cells, not {RoundsHeader.Length}");
            }

            if (!int.TryParse(cells[0], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture,
                    out var round) || round != _rounds.Count + 1)
            {
                throw new RefusalException(number, $"round '{cells[0]}' is out of order: rounds are numbered from 1, one after another");
            }

            if (cells[1].Length == 0)
            {
                throw new RefusalException(number, $"round {round} has no name");
            }

            var began = Date(cells[2]) ?? throw new RefusalException(number, $"round {round}'s Began is not a date written yyyy-MM-dd");

            return new ProjectRound(round, cells[1], began, cells[3], cells[4], number);
        }

        // Decisions is prose the reader otherwise leaves alone, so a table it cannot read gives no date rather than a refusal.
        private void ReadFirstDecision(int index)
        {
            for (var at = index + 1; at < lines.Length && !lines[at].StartsWith("## ", StringComparison.Ordinal); at++)
            {
                var trimmed = lines[at].Trim();

                if (!trimmed.StartsWith('|') || SeparatorRow().IsMatch(trimmed))
                {
                    continue;
                }

                if (Date(Cells(trimmed)[0]) is { } date)
                {
                    _firstDecision = date;

                    return;
                }
            }
        }

        private void CheckRoundLines()
        {
            foreach (var (objective, round, line) in _roundLines)
            {
                if (round > Math.Max(1, _rounds.Count))
                {
                    throw new RefusalException(line, _rounds.Count == 0
                        ? $"objective {objective} names round {round}, but the file has no Rounds table"
                        : $"objective {objective} names round {round}, which is not in the Rounds table");
                }
            }
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
                objective.Pipelines, objective.Rows, objective.Line)
            {
                Round = objective.Round,
            });
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

        public int RoundLine { get; set; }

        public int Round { get; set; } = 1;

        public List<ProjectCriterion> Criteria { get; } = [];

        public List<PipelineKind> Pipelines { get; } = [];

        public List<ProjectWorkstreamRow> Rows { get; } = [];
    }
}
