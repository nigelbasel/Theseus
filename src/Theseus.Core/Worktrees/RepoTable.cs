namespace Theseus.Core.Worktrees;

/// <summary>Reads a project file's <c>## Repos</c> table (<c>| Repo | Role | Autonomy |</c>), which the reader otherwise leaves as text.</summary>
public static class RepoTable
{
    /// <summary>Its rows, in file order; none when the file has no such table.</summary>
    public static IReadOnlyList<RepoTableRow> Read(string? projectText)
    {
        if (string.IsNullOrEmpty(projectText))
        {
            return [];
        }

        var rows = new List<RepoTableRow>();
        var inSection = false;

        foreach (var raw in projectText.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                inSection = string.Equals(line[3..].Trim(), "Repos", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inSection || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

            if (cells.Length < 2 || string.Equals(cells[0], "Repo", StringComparison.OrdinalIgnoreCase)
                || cells[0].All(character => character is '-' or ':' or ' '))
            {
                continue;
            }

            rows.Add(new RepoTableRow([.. RepoEntries.Split(cells[0])], cells[1]));
        }

        return rows;
    }
}
