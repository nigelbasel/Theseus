namespace Theseus.Core.Worktrees;

/// <summary>How a start came by a repo's folder.</summary>
public enum WorktreeKind
{
    /// <summary>Theseus made the worktree for this start.</summary>
    Made,

    /// <summary>The worktree was already at its place, on the row's branch, and was taken as it is (AC 9).</summary>
    Adopted,

    /// <summary>The repo is worked in its clone, with no worktree (AC 4).</summary>
    InPlace,
}
