namespace Theseus.Core.Worktrees;

/// <summary>A card's worktree, found for the clear-up on archive.</summary>
/// <param name="Folder">The worktree.</param>
/// <param name="Repo">Its folder name.</param>
/// <param name="ClonePath">The clone it belongs to, from which it is removed.</param>
/// <param name="Branch">Its branch.</param>
public sealed record RemovableWorktree(string Folder, string Repo, string ClonePath, string? Branch);
