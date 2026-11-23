namespace Theseus.Core.Links;

/// <summary>
/// A project's home code repository and its primary clone on this machine (1.1e AC 6e), for the holdout folder found
/// beside it: the first code repo in the project file's Repos table, else the repository its cards work in.
/// </summary>
public static class HomeRepo
{
    /// <summary>What the convention puts after a repo's name for its holdout folder.</summary>
    public const string HoldoutSuffix = "-holdout";

    /// <summary>
    /// The first code repo's name in a project file's Repos table, or null (review of PR 7, finding 2): a row's first token,
    /// its last path segment; a local notes folder (<c>local/…</c>), a wildcard or a placeholder is no code repo.
    /// </summary>
    public static string? NameIn(string? projectText)
    {
        if (string.IsNullOrEmpty(projectText))
        {
            return null;
        }

        var inRepos = false;

        foreach (var raw in projectText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith('#'))
            {
                inRepos = string.Equals(line, "## Repos", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!inRepos || !line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|');

            if (IsHoldoutRole(cells) || RepoName(cells[0]) is not { } name)
            {
                continue;
            }

            return name;
        }

        return null;
    }

    /// <summary>
    /// The holdout repository a project file records, or null (options-made-clear item 4): a Repos-table row whose role
    /// says holdout, named by its first cell unless that is a description; else a table cell elsewhere, such as a piece's
    /// Repos, that is "the holdout repository".
    /// </summary>
    public static RecordedHoldout? RecordedHoldoutIn(string? projectText)
    {
        if (string.IsNullOrEmpty(projectText))
        {
            return null;
        }

        var inRepos = false;
        RecordedHoldout? mentioned = null;

        foreach (var raw in projectText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith('#'))
            {
                inRepos = string.Equals(line, "## Repos", StringComparison.OrdinalIgnoreCase);

                continue;
            }

            if (!line.StartsWith('|'))
            {
                continue;
            }

            var cells = line.Trim('|').Split('|');

            if (inRepos && IsHoldoutRole(cells))
            {
                var described = cells[0].Split(['`', '('])[0].Trim();

                return new RecordedHoldout(described.Contains(' ', StringComparison.Ordinal) ? null : RepoName(cells[0]));
            }

            if (cells.Any(cell => string.Equals(cell.Trim(), "the holdout repository", StringComparison.OrdinalIgnoreCase)))
            {
                mentioned ??= new RecordedHoldout(null);
            }
        }

        return mentioned;
    }

    private static bool IsHoldoutRole(string[] cells) =>
        cells.Length > 1 && cells[1].Contains("holdout", StringComparison.OrdinalIgnoreCase);

    // A row's first token, its last path segment; a local notes folder, a wildcard, a placeholder or the header is none.
    private static string? RepoName(string cell)
    {
        var token = cell.Trim().Split([' ', '\t', '`', '('], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

        if (token.Length == 0 || token.StartsWith('-') || token.StartsWith('<') || token.Contains('*', StringComparison.Ordinal)
            || string.Equals(token, "Repo", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("local/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = token.TrimEnd('/', ':').Split('/', '\\')[^1];

        return name.Length > 0 && name.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_') ? name : null;
    }

    /// <summary>
    /// The primary clone of the home repo among the folders the project's building cards work in (review of PR 7, finding
    /// 4): the git repository (or a worktree's primary clone) a folder is in, named for the repo when a name is known. The
    /// folders are taken in ordinal order, so the answer does not depend on the board's order. Null when none is found.
    /// </summary>
    public static string? Clone(string? repoName, IEnumerable<string> cardFolders)
    {
        ArgumentNullException.ThrowIfNull(cardFolders);

        foreach (var folder in cardFolders.Where(folder => !string.IsNullOrWhiteSpace(folder))
                     .Select(Path.TrimEndingDirectorySeparator).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (PrimaryClone(folder) is { } clone
                && (repoName is null || string.Equals(Path.GetFileName(clone), repoName, StringComparison.OrdinalIgnoreCase)))
            {
                return clone;
            }
        }

        return null;
    }

    /// <summary>The holdout folder beside a clone, <c>&lt;repo&gt;-holdout</c>, when it is there; otherwise null.</summary>
    public static string? HoldoutBeside(string? clone)
    {
        if (clone is null || Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(clone)) is not { } parent)
        {
            return null;
        }

        var beside = Path.Combine(parent, $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(clone))}{HoldoutSuffix}");

        return Directory.Exists(beside) ? beside : null;
    }

    // The repository a folder is in: the nearest folder with .git; a worktree's .git file leads, through its gitdir and
    // commondir, to the primary clone, beside which the holdout sits.
    private static string? PrimaryClone(string folder)
    {
        for (var at = new DirectoryInfo(folder); at is not null; at = at.Parent)
        {
            var git = Path.Combine(at.FullName, ".git");

            if (Directory.Exists(git))
            {
                return at.FullName;
            }

            if (File.Exists(git))
            {
                return FromWorktree(git) ?? at.FullName;
            }
        }

        return null;
    }

    private static string? FromWorktree(string gitFile)
    {
        try
        {
            var line = File.ReadLines(gitFile).FirstOrDefault(each => each.StartsWith("gitdir:", StringComparison.Ordinal));

            if (line is null)
            {
                return null;
            }

            var gitDir = Path.GetFullPath(line["gitdir:".Length..].Trim(), Path.GetDirectoryName(gitFile)!);
            var common = Path.Combine(gitDir, "commondir");
            var commonDir = File.Exists(common) ? Path.GetFullPath(File.ReadAllText(common).Trim(), gitDir) : gitDir;

            return string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(commonDir)), ".git", StringComparison.OrdinalIgnoreCase)
                ? Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(commonDir))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
