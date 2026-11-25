namespace Theseus.Core.Worktrees;

/// <summary>What a start knows of a card's row and the machine's folders, for preparing its worktrees.</summary>
/// <param name="Slug">The row's slug, naming the folder its worktrees sit in.</param>
/// <param name="Branch">The row's Branch.</param>
/// <param name="ReposCell">The row's Repos, as written.</param>
/// <param name="ProjectText">The project file's text, for its Repos table.</param>
/// <param name="ProjectName">The project's folder name, which a lead's row may write as its home.</param>
/// <param name="PrimaryBranch">The project's primary branch, as its file names it.</param>
/// <param name="ClonesFolder">The clones folder setting.</param>
/// <param name="WorktreesFolder">The worktrees folder setting.</param>
public sealed record WorktreeRequest(
    string Slug,
    string? Branch,
    string? ReposCell,
    string? ProjectText,
    string? ProjectName,
    string? PrimaryBranch,
    string? ClonesFolder,
    string? WorktreesFolder);
