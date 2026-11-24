using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;

namespace Theseus.Core.Worktrees;

/// <summary>
/// Clears up an archived card's worktrees (worktree-per-repo AC 14 to 17). Only what can be shown to be the card's is
/// touched: a worktree in its slug folder, on its row's branch, that no other card still lists. A worktree with
/// uncommitted changes is never removed, and no branch is ever deleted: removing the worktree leaves its branch and
/// every commit on it.
/// </summary>
public sealed class WorktreeClearUp
{
    private readonly IGit _git;

    /// <summary>Creates a clear-up over a git runner.</summary>
    public WorktreeClearUp(IGit git) => _git = git ?? throw new ArgumentNullException(nameof(git));

    /// <summary>The card's worktrees, sorted into those that may be offered and those kept.</summary>
    /// <param name="worktreesFolder">The worktrees folder setting.</param>
    /// <param name="slug">The card's slug.</param>
    /// <param name="branch">The card's row branch; a worktree on another is not the card's.</param>
    /// <param name="usedElsewhere">Whether another card not archived lists a folder among its extra directories.</param>
    public WorktreeClearUpList Find(string? worktreesFolder, string? slug, string? branch, Func<string, bool> usedElsewhere)
    {
        ArgumentNullException.ThrowIfNull(usedElsewhere);

        if (string.IsNullOrWhiteSpace(worktreesFolder) || string.IsNullOrWhiteSpace(slug) || !RowRepos.MakesWorktrees(branch))
        {
            return new WorktreeClearUpList([], []);
        }

        var slugFolder = Path.Combine(worktreesFolder, slug);

        if (!Directory.Exists(slugFolder))
        {
            return new WorktreeClearUpList([], []);
        }

        var clean = new List<RemovableWorktree>();
        var kept = new List<RemovableWorktree>();

        foreach (var folder in Directory.EnumerateDirectories(slugFolder).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(folder, ".git"))
                || !string.Equals(GitBranchReader.Read(folder), branch!.Trim(), StringComparison.Ordinal)
                || usedElsewhere(folder)
                || CloneOf(folder) is not { } clone)
            {
                continue;
            }

            var worktree = new RemovableWorktree(ListedWorktree.Normal(folder), Path.GetFileName(folder), clone, branch.Trim());

            (IsClean(folder) ? clean : kept).Add(worktree);
        }

        return new WorktreeClearUpList(clean, kept);
    }

    /// <summary>Removes one worktree, checked clean again first; null when removed, else why not.</summary>
    public string? Remove(RemovableWorktree worktree)
    {
        ArgumentNullException.ThrowIfNull(worktree);

        if (!IsClean(worktree.Folder))
        {
            return $"{worktree.Folder} has uncommitted changes now, so it was kept.";
        }

        // No --force: git itself refuses a worktree with changes, a second guard behind the check above.
        var removed = _git.Run(worktree.ClonePath, "worktree", "remove", worktree.Folder);

        if (!removed.Succeeded)
        {
            return $"{worktree.Folder} was kept: {removed.FirstErrorLine}";
        }

        TheseusLog.Information("Removed the worktree {Folder} of {ClonePath}; its branch {Branch} is kept", worktree.Folder, worktree.ClonePath, worktree.Branch);

        RemoveIfEmpty(Path.GetDirectoryName(worktree.Folder));

        return null;
    }

    private static void RemoveIfEmpty(string? folder)
    {
        try
        {
            if (folder is not null && Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The empty folder {Folder} could not be removed: {Reason}", folder, exception.Message);
        }
    }

    private bool IsClean(string folder)
    {
        var status = _git.Run(folder, "status", "--porcelain", "--untracked-files=all");

        return status.Succeeded && status.Output.Trim().Length == 0;
    }

    private string? CloneOf(string folder)
    {
        var common = _git.Run(folder, "rev-parse", "--path-format=absolute", "--git-common-dir");

        if (!common.Succeeded || common.Output.Trim() is not { Length: > 0 } gitDir)
        {
            return null;
        }

        var normal = ListedWorktree.Normal(gitDir);

        return string.Equals(Path.GetFileName(normal), ".git", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(normal) : null;
    }
}
