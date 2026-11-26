namespace Theseus.Core.Projects;

/// <summary>
/// The owner's edits to a project file, each the smallest change to its text: a row added or removed, a box ticked, a
/// heading renamed, an objective added; every other byte stays as it was, each line's own ending included (1.1c AC 9).
/// Lines are counted as the reader counts them, so a file with mixed endings is edited on the line the view showed.
/// Sessions edit the same file by hand, so nothing here keeps a copy of what it says. An edit whose target is not in
/// the file, or whose result would not read, is refused with the reason.
/// </summary>
public static class ProjectFileEditor
{
    private static readonly string[] TableHeader = ["Workstream", "Slug", "Branch", "Repos"];

    /// <summary>Adds a workstream row at the end of an objective's table; refused for a slug the file already has.</summary>
    public static string AddRow(string text, string objective, ProjectWorkstreamRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        var file = Open(text, out var lines);

        // The reader refuses a slug listed twice, so a second row for one would leave the whole file unreadable.
        if (file.Workstreams.FirstOrDefault(each => string.Equals(each.Slug, row.Slug, StringComparison.OrdinalIgnoreCase)) is { } taken)
        {
            throw new ProjectEditRefusedException($"the slug {row.Slug} is already in {file.Name}'s table, for {taken.Workstream}");
        }

        var heading = Objective(file, objective);
        var header = FindInObjective(lines, heading.Line, IsTableHeader)
                     ?? throw new ProjectEditRefusedException($"objective {objective} has no workstreams table");
        var last = header + 1;

        while (last + 1 < lines.Count && lines[last + 1].Text.TrimStart().StartsWith('|'))
        {
            last++;
        }

        // A table with a What column takes a fifth cell, or the reader would refuse the row (1.1f B7).
        var what = Cells(lines[header].Text).Length > TableHeader.Length ? $" {row.What} |" : string.Empty;
        lines.Insert(last + 1, NewLine(lines, last, $"| {row.Workstream} | {row.Slug} | {row.Branch} | {row.Repos} |{what}"));

        return Close(lines, file.Name);
    }

    /// <summary>Removes every row naming a card; refused when none does.</summary>
    public static string RemoveRows(string text, string card)
    {
        var file = Open(text, out var lines);
        var rows = file.Workstreams.Where(row => string.Equals(row.Workstream, card, StringComparison.OrdinalIgnoreCase))
            .Select(row => row.Line - 1)
            .OrderDescending()
            .ToList();

        if (rows.Count == 0)
        {
            throw new ProjectEditRefusedException($"no row of {file.Name} names {card}");
        }

        foreach (var index in rows)
        {
            lines.RemoveAt(index);
        }

        return Close(lines, file.Name);
    }

    /// <summary>
    /// Takes out every row naming one of these cards, as Clear up verifiers… does, and returns what Undo needs to put each
    /// back; a card with no row is passed over, so a second try after a refused write takes only what is left.
    /// </summary>
    public static (string Text, IReadOnlyList<ClearedRow> Rows) TakeRows(string text, IReadOnlyCollection<string> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        var file = Open(text, out var lines);
        var taken = new List<ClearedRow>();

        foreach (var objective in file.Objectives)
        {
            for (var index = 0; index < objective.Workstreams.Count; index++)
            {
                var row = objective.Workstreams[index];

                if (cards.Contains(row.Workstream, StringComparer.OrdinalIgnoreCase))
                {
                    taken.Add(new ClearedRow(row.Workstream, objective.Number, row.Slug, lines[row.Line - 1].Text,
                        index == 0 ? null : objective.Workstreams[index - 1].Slug));
                }
            }
        }

        if (taken.Count == 0)
        {
            return (text, taken);
        }

        foreach (var index in file.Workstreams.Where(row => cards.Contains(row.Workstream, StringComparer.OrdinalIgnoreCase))
                     .Select(row => row.Line - 1).OrderDescending())
        {
            lines.RemoveAt(index);
        }

        return (Close(lines, file.Name), taken);
    }

    /// <summary>
    /// Puts rows Clear up took back, in the order taken: each under the row that was above it, or at the end of its table
    /// when that row has gone. A row whose slug the file has again is left as it is.
    /// </summary>
    public static string PutBackRows(string text, IReadOnlyList<ClearedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        foreach (var row in rows)
        {
            var file = Open(text, out var lines);

            if (file.ObjectiveOf(row.Slug) is not null)
            {
                continue;
            }

            var objective = Objective(file, row.Objective);
            var header = FindInObjective(lines, objective.Line, IsTableHeader)
                         ?? throw new ProjectEditRefusedException($"objective {row.Objective} has no workstreams table");
            var last = objective.Workstreams.Count > 0 ? objective.Workstreams[^1].Line - 1 : header + 1;
            var after = row.After is null
                ? header + 1
                : objective.Workstreams.FirstOrDefault(each => string.Equals(each.Slug, row.After, StringComparison.OrdinalIgnoreCase)) is { } above
                    ? above.Line - 1
                    : last;

            lines.Insert(after + 1, NewLine(lines, after, row.Text));
            text = Close(lines, file.Name);
        }

        return text;
    }

    /// <summary>Ticks or unticks a criterion, by its line.</summary>
    public static string SetCriterion(string text, int line, bool ticked)
    {
        var file = Open(text, out var lines);

        var box = file.Objectives.SelectMany(objective => objective.Criteria).Any(criterion => criterion.Line == line) && line <= lines.Count
            ? lines[line - 1].Text.IndexOf("- [", StringComparison.Ordinal)
            : -1;

        if (box < 0)
        {
            throw new ProjectEditRefusedException($"line {line} is not a criterion");
        }

        var chars = lines[line - 1].Text.ToCharArray();
        chars[box + 3] = ticked ? 'x' : ' ';
        lines[line - 1] = lines[line - 1] with { Text = new string(chars) };

        return Close(lines, file.Name);
    }

    /// <summary>
    /// Ticks or unticks a criterion as a view showed it: the objective by number, the criterion by its place there, and
    /// its text as shown checked against the file as it is now. A view can be older than the file (it is redrawn only
    /// when what it draws changes), so a line number it holds may name another line by now (1.1d PR 4 review, finding
    /// 1). Refused when the criterion there no longer reads as shown.
    /// </summary>
    public static string SetCriterion(string text, string objective, int index, string shown, bool ticked)
    {
        var heading = Objective(Open(text, out _), objective);

        if (index < 0 || index >= heading.Criteria.Count || !string.Equals(heading.Criteria[index].Text, shown, StringComparison.Ordinal))
        {
            throw new ProjectEditRefusedException(
                $"criterion {index + 1} of objective {objective} no longer reads \"{shown}\" in the file; it is shown again as the file has it");
        }

        return SetCriterion(text, heading.Criteria[index].Line, ticked);
    }

    /// <summary>
    /// Renames an objective as a view showed it: refused when the objective of that number no longer has the title shown,
    /// as when the file was renumbered since the view was drawn (1.1d PR 4 review, finding 1).
    /// </summary>
    public static string RenameObjective(string text, string objective, string shown, string title)
    {
        var heading = Objective(Open(text, out _), objective);

        if (!string.Equals(heading.Title, shown, StringComparison.Ordinal))
        {
            throw new ProjectEditRefusedException(
                $"objective {objective} is now \"{heading.Title}\", not \"{shown}\"; it is shown again as the file has it");
        }

        return RenameObjective(text, objective, title);
    }

    /// <summary>
    /// Gives the project a new title, its first line's <c># </c> heading (1.1e AC 12a): refused when the file's title is no
    /// longer the one shown, as when it changed since the dialog opened, or when the new title is empty.
    /// </summary>
    public static string SetTitle(string text, string shown, string title)
    {
        var file = Open(text, out var lines);
        var clean = (title ?? string.Empty).Trim();

        if (clean.Length == 0)
        {
            throw new ProjectEditRefusedException("a project needs a title");
        }

        if (!string.Equals(file.Title, shown, StringComparison.Ordinal))
        {
            throw new ProjectEditRefusedException($"project.md's title is now \"{file.Title}\", not \"{shown}\": it changed since this was opened");
        }

        var at = lines.FindIndex(line => line.Text.Trim().Length > 0);
        lines[at] = lines[at] with { Text = $"# {OneLine(clean, "a project's title")}" };

        return Close(lines, file.Name);
    }

    /// <summary>Renames an objective, keeping its number.</summary>
    public static string RenameObjective(string text, string objective, string title)
    {
        var file = Open(text, out var lines);
        var heading = Objective(file, objective);

        lines[heading.Line - 1] = lines[heading.Line - 1] with { Text = $"### Objective {heading.Number}: {OneLine(title, "an objective's title")}" };

        return Close(lines, file.Name);
    }

    /// <summary>
    /// Adds an objective at the end of a phase, numbered after its last, with its criteria, its pipeline (still to be
    /// chosen when none is given) and an empty workstreams table. Returns the text and the new objective's number.
    /// </summary>
    public static (string Text, string Number) AddObjective(string text, int phase, string title, IReadOnlyList<string> criteria,
        PipelineKind? pipeline = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var file = Open(text, out var lines);
        var section = file.Phases.FirstOrDefault(each => each.Number == phase)
                      ?? throw new ProjectEditRefusedException($"{file.Name} has no phase {phase}");
        var next = section.Objectives.Count == 0
            ? 1
            : section.Objectives.Max(objective => int.Parse(objective.Number.Split('.')[1], System.Globalization.CultureInfo.InvariantCulture)) + 1;
        var number = $"{phase}.{next}";

        // The phase ends where the next ## heading outside a fence starts, or at the end of the file; blank lines before
        // it stay where they are.
        var end = Headings(lines).FirstOrDefault(index => index >= section.Line && lines[index].Text.StartsWith("## ", StringComparison.Ordinal), lines.Count);

        while (end > section.Line && lines[end - 1].Text.Trim().Length == 0)
        {
            end--;
        }

        // The file's last line may have no ending; it gets one now that lines follow it.
        if (end == lines.Count && lines[^1].Ending.Length == 0)
        {
            lines[^1] = lines[^1] with { Ending = Ending(lines) };
        }

        string[] block =
        [
            string.Empty,
            $"### Objective {number}: {OneLine(title, "an objective's title")}",
            string.Empty,
            .. criteria.Where(criterion => criterion.Trim().Length > 0).Select(criterion => $"- [ ] {OneLine(criterion, "a criterion")}"),
            string.Empty,
            $"**Pipeline:** {(pipeline is { } chosen ? chosen.ToString().ToLowerInvariant() : "[to be chosen]")}",
            "| Workstream | Slug | Branch | Repos |",
            "|---|---|---|---|",
        ];

        lines.InsertRange(end, block.Select(each => new Line(each, Ending(lines))));

        return (Close(lines, file.Name), number);
    }

    /// <summary>The line an objective has in place of criteria not yet written.</summary>
    public const string CriteriaPlaceholder = "[criteria to be written]";

    /// <summary>
    /// Writes criteria into an existing objective, unticked: in place of its <see cref="CriteriaPlaceholder"/>, else
    /// before its Pipeline line, else after its last criterion and that criterion's notes. No existing line is changed,
    /// moved or removed but the placeholder, so ticked criteria and their Evidence lines are always kept. A criterion the
    /// objective already has is refused, so a retried call cannot write it twice. Returns the text and the place, from 0,
    /// of the first criterion written.
    /// </summary>
    public static (string Text, int First) SetCriteria(string text, string objective, IReadOnlyList<string> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var file = Open(text, out var lines);
        var heading = Objective(file, objective);
        var clean = criteria.Select(criterion => OneLine(criterion, "a criterion")).ToList();

        if (clean.Count == 0)
        {
            throw new ProjectEditRefusedException("set_criteria needs at least one criterion");
        }

        if (clean.FirstOrDefault(criterion => heading.Criteria.Any(each => each.Text == criterion)
                                              || clean.Count(each => each == criterion) > 1) is { } twice)
        {
            throw new ProjectEditRefusedException($"objective {objective} would have the criterion \"{twice}\" twice");
        }

        var stop = Headings(lines).FirstOrDefault(index => index >= heading.Line, lines.Count);

        if (heading.Criteria.Count > 0)
        {
            var at = heading.Criteria[^1].Line - 1;
            var indent = lines[at].Text[..lines[at].Text.IndexOf("- [", StringComparison.Ordinal)];

            while (at + 1 < lines.Count && lines[at + 1].Text.Length > 0 && char.IsWhiteSpace(lines[at + 1].Text[0])
                   && lines[at + 1].Text.Trim().Length > 0 && !lines[at + 1].Text.TrimStart().StartsWith("- [", StringComparison.Ordinal))
            {
                at++;
            }

            foreach (var criterion in clean)
            {
                lines.Insert(at + 1, NewLine(lines, at, $"{indent}- [ ] {criterion}"));
                at++;
            }

            return (Close(lines, file.Name), heading.Criteria.Count);
        }

        var boxes = clean.Select(criterion => $"- [ ] {criterion}").ToList();
        var placeholder = Enumerable.Range(heading.Line, Math.Max(0, stop - heading.Line))
            .Where(index => lines[index].Text.TrimEnd().EndsWith(CriteriaPlaceholder, StringComparison.Ordinal))
            .DefaultIfEmpty(-1)
            .First();

        if (placeholder >= 0)
        {
            var ending = lines[placeholder].Ending.Length > 0 ? lines[placeholder].Ending : Ending(lines);
            var prose = lines[placeholder].Text.TrimEnd()[..^CriteriaPlaceholder.Length].TrimEnd();

            if (prose.Length == 0)
            {
                lines.RemoveAt(placeholder);
            }
            else
            {
                lines[placeholder] = lines[placeholder] with { Text = prose, Ending = ending };
                placeholder++;
            }

            lines.InsertRange(placeholder, boxes.Select(box => new Line(box, ending)));

            return (Close(lines, file.Name), 0);
        }

        // Before the Pipeline line and the blank line above it, so a Round line stays directly under the heading.
        var before = FindInObjective(lines, heading.Line, line => line.StartsWith("**Pipeline:**", StringComparison.Ordinal))
                     ?? throw new ProjectEditRefusedException($"objective {objective} has no **Pipeline:** line");

        if (before - 1 > heading.Line - 1 && lines[before - 1].Text.Trim().Length == 0)
        {
            before--;
        }
        else
        {
            boxes.Add(string.Empty);
        }

        lines.InsertRange(before, boxes.Select(box => new Line(box, Ending(lines))));

        return (Close(lines, file.Name), 0);
    }

    /// <summary>
    /// Sets or clears a criterion's note line (its <c>Waiting:</c> or <c>Evidence:</c> line), addressed as a view showed it
    /// and refused when the criterion no longer reads that way (brief, PR 2: Theseus writes the structured lines).
    /// </summary>
    /// <param name="text">The project file's text.</param>
    /// <param name="objective">The objective's number.</param>
    /// <param name="index">The criterion's place in the objective, from 0.</param>
    /// <param name="shown">The criterion's text, checked against the file.</param>
    /// <param name="label">The note's prefix, such as <c>Waiting:</c>.</param>
    /// <param name="value">What follows it, or null to remove the note.</param>
    public static string SetCriterionNote(string text, string objective, int index, string shown, string label, string? value)
    {
        var file = Open(text, out var lines);
        var heading = Objective(file, objective);

        if (index < 0 || index >= heading.Criteria.Count || !string.Equals(heading.Criteria[index].Text, shown, StringComparison.Ordinal))
        {
            throw new ProjectEditRefusedException($"criterion {index + 1} of objective {objective} does not read \"{shown}\" in the file");
        }

        var at = heading.Criteria[index].Line - 1;
        var last = at;

        while (last + 1 < lines.Count && lines[last + 1].Text.Length > 0 && char.IsWhiteSpace(lines[last + 1].Text[0])
               && lines[last + 1].Text.Trim().Length > 0 && !lines[last + 1].Text.TrimStart().StartsWith("- [", StringComparison.Ordinal))
        {
            last++;
        }

        for (var line = last; line > at; line--)
        {
            if (lines[line].Text.TrimStart().StartsWith(label, StringComparison.Ordinal))
            {
                lines.RemoveAt(line);
                last--;
            }
        }

        if (value is not null)
        {
            var indent = new string(' ', lines[at].Text.IndexOf("- [", StringComparison.Ordinal) + 6);
            lines.Insert(last + 1, NewLine(lines, last, $"{indent}{label} {OneLine(value, "a note")}"));
        }

        return Close(lines, file.Name);
    }

    /// <summary>
    /// Records a new round: a row of the Rounds table, made after the Acceptance criteria section when the file has none
    /// (with round 1 for the work so far), and a Round line on each objective the round adds or reopens (1.1f B6, B9).
    /// Returns the text and the new round's number.
    /// </summary>
    public static (string Text, int Number) AddRound(string text, string name, string why, string by, DateOnly began,
        IReadOnlyList<string> objectives)
    {
        ArgumentNullException.ThrowIfNull(objectives);

        var file = Open(text, out var lines);
        var cleanName = OneLine(name, "a round's name");
        var cleanWhy = OneLine(why, "why the requirements changed");
        var cleanBy = OneLine(by, "who changed them");

        foreach (var named in objectives)
        {
            Objective(file, named);
        }

        if (objectives.Count == 0)
        {
            throw new ProjectEditRefusedException("a round names the objectives it adds or reopens");
        }

        string Row(int round, string roundName, DateOnly date, string roundWhy, string roundBy) =>
            $"| {round} | {Cell(roundName)} | {date:yyyy-MM-dd} | {Cell(roundWhy)} | {Cell(roundBy)} |";

        int number;

        if (file.RoundsTable.Count > 0)
        {
            number = file.RoundsTable.Count + 1;
            var lastRow = file.RoundsTable[^1].Line - 1;
            lines.Insert(lastRow + 1, NewLine(lines, lastRow, Row(number, cleanName, began, cleanWhy, cleanBy)));
        }
        else
        {
            number = 2;
            var first = file.Rounds[0];
            var at = RoundsPlace(lines);
            string[] block =
            [
                "## Rounds",
                string.Empty,
                "| Round | Name | Began | Why | By |",
                "|---|---|---|---|---|",
                Row(1, "Round 1", first.Began ?? began, "The first requirements", cleanBy),
                Row(2, cleanName, began, cleanWhy, cleanBy),
                string.Empty,
            ];
            lines.InsertRange(at, block.Select(each => new Line(each, Ending(lines))));
        }

        // The Round lines go in by heading text, since the rows above moved every line number.
        foreach (var objective in objectives)
        {
            var heading = Headings(lines).First(index => lines[index].Text.StartsWith($"### Objective {objective}:", StringComparison.Ordinal));

            if (heading + 1 < lines.Count && lines[heading + 1].Text.StartsWith("**Round:**", StringComparison.Ordinal))
            {
                lines[heading + 1] = lines[heading + 1] with { Text = $"**Round:** {number}" };
            }
            else
            {
                lines.Insert(heading + 1, NewLine(lines, heading, $"**Round:** {number}"));
            }
        }

        return (Close(lines, file.Name), number);
    }

    /// <summary>Adds a dated row at the end of the Decisions table, making the table at the file's end when there is none.</summary>
    public static string AddDecision(string text, DateOnly date, string decision, string by)
    {
        var file = Open(text, out var lines);
        var row = $"| {date:yyyy-MM-dd} | {Cell(OneLine(decision, "a decision"))} | {Cell(OneLine(by, "who decided"))} |";
        var section = Headings(lines).Where(index => lines[index].Text.TrimEnd() == "## Decisions").DefaultIfEmpty(-1).First();

        if (section < 0)
        {
            if (lines[^1].Ending.Length == 0)
            {
                lines[^1] = lines[^1] with { Ending = Ending(lines) };
            }

            string[] block = [string.Empty, "## Decisions", string.Empty, "| Date | Decision | By |", "|---|---|---|", row];
            lines.AddRange(block.Select(each => new Line(each, Ending(lines))));

            return Close(lines, file.Name);
        }

        var header = section + 1;

        while (header < lines.Count && !lines[header].Text.TrimStart().StartsWith('|'))
        {
            if (lines[header].Text.StartsWith('#'))
            {
                throw new ProjectEditRefusedException("the Decisions section has no table");
            }

            header++;
        }

        var last = header;

        while (last + 1 < lines.Count && lines[last + 1].Text.TrimStart().StartsWith('|'))
        {
            last++;
        }

        lines.Insert(last + 1, NewLine(lines, last, row));

        return Close(lines, file.Name);
    }

    // After the Acceptance criteria section, else before the first phase (1.1f B6).
    private static int RoundsPlace(List<Line> lines)
    {
        var headings = Headings(lines).Where(index => lines[index].Text.StartsWith("## ", StringComparison.Ordinal)).ToList();
        var criteria = headings.FindIndex(index => lines[index].Text.TrimEnd() == "## Acceptance criteria");

        if (criteria >= 0 && criteria + 1 < headings.Count)
        {
            return headings[criteria + 1];
        }

        return headings.FirstOrDefault(index => lines[index].Text.StartsWith("## Phase", StringComparison.Ordinal), lines.Count);
    }

    private static string Cell(string text) => text.Replace("|", "/", StringComparison.Ordinal);

    private sealed record Line(string Text, string Ending);

    // Lines as the reader counts them: split at each line feed, a carriage return before it kept as that line's ending.
    private static ProjectFile Open(string text, out List<Line> lines)
    {
        ArgumentNullException.ThrowIfNull(text);

        var reading = ProjectFileReader.Read("project", text);

        if (reading.File is not { } file)
        {
            throw new ProjectEditRefusedException($"the file does not read: {reading.Reason}");
        }

        lines = [];

        var start = 0;

        while (start <= text.Length)
        {
            var feed = text.IndexOf('\n', start);

            if (feed < 0)
            {
                lines.Add(new Line(text[start..], string.Empty));

                break;
            }

            var cr = feed > start && text[feed - 1] == '\r';
            lines.Add(new Line(text[start..(cr ? feed - 1 : feed)], cr ? "\r\n" : "\n"));
            start = feed + 1;
        }

        return file;
    }

    // The edited text, refused when it would not read: an edit must never leave the owner's file broken.
    private static string Close(List<Line> lines, string name)
    {
        var text = string.Concat(lines.Select(line => line.Text + line.Ending));
        var reading = ProjectFileReader.Read(name, text);

        return reading.File is not null
            ? text
            : throw new ProjectEditRefusedException($"the edit would leave project.md unreadable ({reading.Reason}), so it was not made");
    }

    // A new line takes the ending of the line it follows, so a file keeps its own style.
    // The line it follows may be the file's last, with no ending: it gets one first, or the two would run together.
    private static Line NewLine(List<Line> lines, int after, string text)
    {
        var ending = lines[after].Ending.Length > 0 ? lines[after].Ending : Ending(lines);

        if (lines[after].Ending.Length == 0)
        {
            lines[after] = lines[after] with { Ending = ending };
        }

        return new Line(text, after == lines.Count - 1 ? string.Empty : ending);
    }

    private static string Ending(List<Line> lines) => lines.Any(line => line.Ending == "\r\n") ? "\r\n" : "\n";

    private static ProjectObjective Objective(ProjectFile file, string number) =>
        file.Objectives.FirstOrDefault(objective => objective.Number == number)
        ?? throw new ProjectEditRefusedException($"there is no objective {number}");

    // Headings outside code fences, which the reader skips too.
    private static IEnumerable<int> Headings(List<Line> lines)
    {
        var inFence = false;

        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Text.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                inFence = !inFence;
            }
            else if (!inFence && lines[index].Text.StartsWith("#", StringComparison.Ordinal))
            {
                yield return index;
            }
        }
    }

    // The first line under an objective's heading that matches, before the next heading outside a fence.
    private static int? FindInObjective(List<Line> lines, int headingLine, Func<string, bool> matches)
    {
        var stop = Headings(lines).FirstOrDefault(index => index >= headingLine, lines.Count);

        for (var index = headingLine; index < stop; index++)
        {
            if (matches(lines[index].Text))
            {
                return index;
            }
        }

        return null;
    }

    // The header as the reader accepts it: its cells, whatever the spacing.
    private static bool IsTableHeader(string line)
    {
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('|'))
        {
            return false;
        }

        var cells = Cells(trimmed);

        return cells.SequenceEqual(TableHeader, StringComparer.Ordinal)
               || (cells.Length == TableHeader.Length + 1 && cells.Take(TableHeader.Length).SequenceEqual(TableHeader, StringComparer.Ordinal)
                   && cells[^1] == "What");
    }

    private static string[] Cells(string line) => line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static string OneLine(string text, string what)
    {
        var clean = (text ?? string.Empty).Trim();

        if (clean.Length == 0 || clean.Contains('\n') || clean.Contains('\r'))
        {
            throw new ProjectEditRefusedException($"{what} must be one line of text");
        }

        return clean;
    }
}
