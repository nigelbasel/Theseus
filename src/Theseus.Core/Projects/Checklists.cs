namespace Theseus.Core.Projects;

/// <summary>The checklists Theseus asks before acting on several cards at once.</summary>
public static class Checklists
{
    /// <summary>Clear up verifiers…: one row per card that should not be kept, saying what it checks, why, and whether it runs.</summary>
    /// <param name="wrong">The cards.</param>
    /// <param name="titleOf">A card's title from its key, for the card each checks.</param>
    public static Checklist ClearUp(IReadOnlyList<WrongVerifier> wrong, Func<string, string> titleOf)
    {
        ArgumentNullException.ThrowIfNull(wrong);
        ArgumentNullException.ThrowIfNull(titleOf);

        var running = wrong.Where(card => card.IsRunning).Select(card => card.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new Checklist("Clear up cards",
            "Each card ticked is stopped if it is running, then archived; every report it filed is rejected, a confirmed one too, "
            + "so nothing it said counts as met. The newest correct verifier of each build is kept. You can undo this.",
            "Clear up",
            [
                .. wrong.Select(card => new ChecklistRow(card.Key, card.Title,
                    $"{(card.IsWriter ? $"Writes scenarios for {card.Subject}" : $"Checks {titleOf(card.Subject)}")}"
                    // The card's key as well: copies share their title, and the rows must be told apart.
                    + $" · {(card.IsRunning ? "running" : "not running")} · card {card.Key}",
                    Sentence(card.Why))),
            ],
            ticked =>
            {
                var stop = ticked.Count(running.Contains);

                return ticked.Count == 0 ? "Nothing ticked"
                    : stop == 0 ? $"Archive {ticked.Count}"
                    : $"Stop {stop} and archive {ticked.Count}";
            });
    }

    /// <summary>
    /// Remove worktrees… on archive (worktree-per-repo AC 14, 15): one row per clean worktree, each ticked to begin with. A
    /// worktree with changes is no row, since it is never removed, and the intro names it as kept.
    /// </summary>
    /// <param name="clean">The worktrees that may be removed; each row's key is its folder.</param>
    /// <param name="kept">The worktrees kept for their changes.</param>
    public static Checklist RemoveWorktrees(IReadOnlyList<Theseus.Core.Worktrees.RemovableWorktree> clean,
        IReadOnlyList<Theseus.Core.Worktrees.RemovableWorktree> kept)
    {
        ArgumentNullException.ThrowIfNull(clean);
        ArgumentNullException.ThrowIfNull(kept);

        return new Checklist("Remove worktrees",
            "The card is archived. Each worktree ticked is removed from its clone; its branch and every commit on it are kept, "
            + "and nothing with uncommitted changes, or ignored files other than build output, is touched."
            + (kept.Count == 0 ? string.Empty : $" Kept for their uncommitted changes or ignored files: {Folders(kept)}."),
            "Remove",
            [.. clean.Select(worktree => new ChecklistRow(worktree.Folder, worktree.Repo, $"{worktree.Folder} · on {worktree.Branch} · clean"))],
            ticked => ticked.Count switch
            {
                0 => "Nothing ticked",
                1 => "Remove 1 worktree",
                _ => $"Remove {ticked.Count} worktrees",
            });
    }

    /// <summary>Worktrees' folders, for a sentence.</summary>
    public static string Folders(IEnumerable<Theseus.Core.Worktrees.RemovableWorktree> worktrees) =>
        string.Join(", ", worktrees.Select(worktree => worktree.Folder));

    private static string Sentence(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..].TrimEnd('.') + ".";
}
