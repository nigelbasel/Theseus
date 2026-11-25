namespace Theseus.Core.Worktrees;

/// <summary>What preparing a card's worktrees came to: the repos ready, or why the card must not start.</summary>
/// <param name="Repos">The repos ready, in the row's order; empty when nothing was asked of Theseus.</param>
/// <param name="Branch">The row's branch.</param>
/// <param name="Refusal">Why the card does not start, or null when it may.</param>
public sealed record WorktreePreparation(IReadOnlyList<PreparedRepo> Repos, string? Branch, string? Refusal)
{
    /// <summary>Nothing to prepare: the card starts exactly as before (AC 1).</summary>
    public static WorktreePreparation Nothing { get; } = new([], null, null);

    /// <summary>A refusal.</summary>
    public static WorktreePreparation Refused(string reason, IReadOnlyList<PreparedRepo>? made = null) => new(made ?? [], null, reason);

    /// <summary>Whether the card may start.</summary>
    public bool MayStart => Refusal is null;

    /// <summary>The folders to add to the card's extra directories, in order (AC 12).</summary>
    public IReadOnlyList<string> Folders => [.. Repos.Select(repo => repo.Folder)];

    /// <summary>A few words for the start notice, or null when there is nothing to say (AC 13).</summary>
    public string? Notice
    {
        get
        {
            if (!MayStart || Repos.Count == 0)
            {
                return null;
            }

            var parts = new List<string>();
            var made = Repos.Count(repo => repo.Kind == WorktreeKind.Made);
            var adopted = Repos.Count(repo => repo.Kind == WorktreeKind.Adopted);
            var inPlace = Repos.Where(repo => repo.Kind == WorktreeKind.InPlace).ToList();

            if (made > 0)
            {
                parts.Add($"{made} {(made == 1 ? "worktree" : "worktrees")} made");
            }

            if (adopted > 0)
            {
                parts.Add($"{adopted} already there");
            }

            parts.AddRange(inPlace.Select(repo => OffBranch(repo)
                ? $"{repo.Repo} worked in place on {repo.Branch ?? "an unknown branch"}, not {Branch}"
                : $"{repo.Repo} worked in place"));

            return $"on {Branch}: {string.Join(", ", parts)}";
        }
    }

    /// <summary>What the session is told at its opening, fresh or resumed (AC 13); null when there is nothing to say.</summary>
    public string? Opening
    {
        get
        {
            if (!MayStart || Repos.Count == 0)
            {
                return null;
            }

            var lines = Repos.Select(repo => repo.Kind switch
            {
                WorktreeKind.Made => $"- {repo.Repo}: {repo.Folder}, on {repo.Branch} (Theseus made this worktree for this start)",
                WorktreeKind.Adopted => $"- {repo.Repo}: {repo.Folder}, on {repo.Branch} (already there)",
                _ => OffBranch(repo)
                    ? $"- {repo.Repo}: {repo.Folder}, worked in place, not in a worktree; it is on {repo.Branch ?? "an unknown branch"}, not {Branch}"
                    : $"- {repo.Repo}: {repo.Folder}, worked in place, not in a worktree",
            });

            return $"Your repos are ready, one folder each, on the branch {Branch}:\n{string.Join('\n', lines)}";
        }
    }

    private bool OffBranch(PreparedRepo repo) => !string.Equals(repo.Branch, Branch, StringComparison.Ordinal);
}
