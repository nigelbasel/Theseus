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

        lines.Insert(last + 1, NewLine(lines, last, $"| {row.Workstream} | {row.Slug} | {row.Branch} | {row.Repos} |"));

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

    /// <summary>Renames an objective, keeping its number.</summary>
    public static string RenameObjective(string text, string objective, string title)
    {
        var file = Open(text, out var lines);
        var heading = Objective(file, objective);

        lines[heading.Line - 1] = lines[heading.Line - 1] with { Text = $"### Objective {heading.Number}: {OneLine(title, "an objective's title")}" };

        return Close(lines, file.Name);
    }

    /// <summary>
    /// Adds an objective at the end of a phase, numbered after its last, with its criteria, a pipeline still to be chosen
    /// and an empty workstreams table. Returns the text and the new objective's number.
    /// </summary>
    public static (string Text, string Number) AddObjective(string text, int phase, string title, IReadOnlyList<string> criteria)
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
            "**Pipeline:** [to be chosen]",
            "| Workstream | Slug | Branch | Repos |",
            "|---|---|---|---|",
        ];

        lines.InsertRange(end, block.Select(each => new Line(each, Ending(lines))));

        return (Close(lines, file.Name), number);
    }

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
    private static Line NewLine(List<Line> lines, int after, string text) =>
        new(text, lines[after].Ending.Length > 0 ? lines[after].Ending : Ending(lines));

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

        var cells = trimmed.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

        return cells.SequenceEqual(TableHeader, StringComparer.Ordinal);
    }

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
