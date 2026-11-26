namespace Theseus.Core.Settings;

/// <summary>Which branch rules the owner granted a project in Project properties.</summary>
public enum BranchGrantKind
{
    /// <summary>A card branches off the project's primary branch, pushes, and merges back into it (1.1e AC 12b).</summary>
    MergeIntoPrimary,

    /// <summary>
    /// Each card's item gets its own branch off its repository's trunk, pushed, with a pull request into the trunk that the
    /// owner completes; the primary branch carries no code (2.4 W2, from Bug Fixing's decision of 2026-11-24).
    /// </summary>
    ItemBranches,
}
