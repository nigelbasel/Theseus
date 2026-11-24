namespace Theseus.Core.Worktrees;

/// <summary>A card's worktrees as the clear-up finds them: those it may offer, and those it keeps (AC 14, 15).</summary>
/// <param name="Clean">Clean, used by no other card: offered for removal.</param>
/// <param name="Kept">With uncommitted changes or untracked files, or whose state could not be read: never offered.</param>
public sealed record WorktreeClearUpList(IReadOnlyList<RemovableWorktree> Clean, IReadOnlyList<RemovableWorktree> Kept)
{
    /// <summary>Whether there is anything to say.</summary>
    public bool IsEmpty => Clean.Count == 0 && Kept.Count == 0;
}
