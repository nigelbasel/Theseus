namespace Theseus.Core.Worktrees;

/// <summary>One repo a start has ready for its session.</summary>
/// <param name="Repo">The repo's folder name.</param>
/// <param name="ClonePath">Its clone.</param>
/// <param name="Folder">Where the session works on it: its worktree, or the clone when worked in place.</param>
/// <param name="Branch">The branch that folder has checked out.</param>
/// <param name="Kind">Whether it was made, adopted or is worked in place.</param>
public sealed record PreparedRepo(string Repo, string ClonePath, string Folder, string? Branch, WorktreeKind Kind);
