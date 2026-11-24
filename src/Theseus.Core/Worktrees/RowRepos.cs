using Theseus.Core.Projects;

namespace Theseus.Core.Worktrees;

/// <summary>The code repos a card's project row names, with what the project's Repos table says of each (AC 1 to 4).</summary>
public static class RowRepos
{
    /// <summary>
    /// Whether a row's branch is one Theseus makes worktrees on: a branch name that is not <c>none</c>, a placeholder or a
    /// trunk. A row on a trunk, or on none, is worked as it always was (AC 1).
    /// </summary>
    public static bool MakesWorktrees(string? branch) => ProjectBranchRules.HasPrimary(branch);

    /// <summary>
    /// The repos, in the row's order. The project's home is left out: a Repos row whose role is home, or an entry that is
    /// the project's own folder name, as a lead's row writes its home (a lead row naming its project's folder).
    /// </summary>
    public static IReadOnlyList<RowRepo> For(string? reposCell, string? projectText, string? projectName = null)
    {
        var table = RepoTable.Read(projectText);
        var repos = new List<RowRepo>();

        foreach (var entry in RepoEntries.Split(reposCell))
        {
            var row = table.FirstOrDefault(each => each.Match(entry) is not null);

            if (row is { IsHome: true }
                || (projectName is not null && string.Equals(entry, projectName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var reference = row?.Match(entry) is { } matched && RepoEntries.Segments(matched).Count > RepoEntries.Segments(entry).Count
                ? matched
                : entry;

            repos.Add(new RowRepo(entry, reference, row?.IsWorkedInPlace ?? false));
        }

        return repos;
    }
}
