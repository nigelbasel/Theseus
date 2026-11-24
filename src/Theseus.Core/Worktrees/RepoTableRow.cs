namespace Theseus.Core.Worktrees;

/// <summary>One row of a project file's <c>## Repos</c> table.</summary>
/// <param name="References">The repos its first cell names, as written, parentheses and backticks dropped.</param>
/// <param name="Role">Its Role cell, as written.</param>
public sealed record RepoTableRow(IReadOnlyList<string> References, string Role)
{
    /// <summary>Whether it is the project's home, its records, not code (worktree-per-repo AC 3).</summary>
    public bool IsHome => Role.TrimStart().StartsWith("home", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether its repos are worked in place, with no worktree (AC 4). Said by the project, not known from a repo's name:
    /// Core's own rule changed on 2026-11-24, when worktrees became allowed for its unit-test fixes.
    /// </summary>
    public bool IsWorkedInPlace => Role.Contains("worked in place", StringComparison.OrdinalIgnoreCase);

    /// <summary>The reference of this row an entry names, by the whole reference or its last path segment; null for none.</summary>
    public string? Match(string entry) =>
        References.FirstOrDefault(reference => string.Equals(reference, entry, StringComparison.OrdinalIgnoreCase))
        ?? References.FirstOrDefault(reference => string.Equals(RepoEntries.LastSegment(reference), entry, StringComparison.OrdinalIgnoreCase));
}
