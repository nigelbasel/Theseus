using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.Core.Worktrees;

/// <summary>
/// Has a card's repos ready before its session opens (worktree-per-repo, objective 2.1): a worktree per repo at
/// <c>&lt;worktrees&gt;\&lt;slug&gt;\&lt;repo&gt;</c>, all on the row's branch, with each clone found by the layout rule.
/// Every repo is checked before any worktree is made, so a refusal found by checking leaves nothing half made (AC 9).
/// It never clones, fetches, checks out, pushes or deletes a branch (AC 7, 11).
/// </summary>
public sealed class WorktreePreparer
{
    private readonly IGit _git;
    private readonly IGitRemoteReader? _remotes;

    /// <summary>Creates a preparer over a git runner.</summary>
    public WorktreePreparer(IGit git, IGitRemoteReader? remotes = null)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _remotes = remotes;
    }

    /// <summary>Checks and makes what the card's row asks for, or says why it must not start.</summary>
    public WorktreePreparation Prepare(WorktreeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!RowRepos.MakesWorktrees(request.Branch)
            || string.IsNullOrWhiteSpace(request.ClonesFolder)
            || string.IsNullOrWhiteSpace(request.WorktreesFolder)
            || string.IsNullOrWhiteSpace(request.Slug))
        {
            return WorktreePreparation.Nothing;
        }

        var repos = RowRepos.For(request.ReposCell, request.ProjectText, request.ProjectName);

        if (repos.Count == 0)
        {
            return WorktreePreparation.Nothing;
        }

        var branch = request.Branch!.Trim();
        var locator = new CloneLocator(request.ClonesFolder, _remotes);
        var steps = new List<Step>();

        foreach (var repo in repos)
        {
            var (refusal, step) = Check(repo, branch, locator, request);

            if (refusal is not null)
            {
                return WorktreePreparation.Refused(refusal);
            }

            steps.Add(step!);
        }

        if (steps.Where(step => step.Kind != WorktreeKind.InPlace).GroupBy(step => step.Folder, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1) is { } clash)
        {
            return WorktreePreparation.Refused(
                $"Two repos would share the worktree {clash.Key}: {string.Join(" and ", clash.Select(step => step.ClonePath))}.");
        }

        var ready = new List<PreparedRepo>();

        foreach (var step in steps)
        {
            if (step.Add is { } arguments)
            {
                var added = _git.Run(step.ClonePath, arguments);

                if (!added.Succeeded)
                {
                    TheseusLog.Warning("git worktree add for {Folder} failed: {Error}", step.Folder, added.Error);

                    return WorktreePreparation.Refused(
                        $"The worktree {step.Folder} could not be made: {added.FirstErrorLine}"
                        + (ready.Any(each => each.Kind == WorktreeKind.Made) ? " The worktrees already made stay, and are taken as they are next time." : string.Empty),
                        ready);
                }

                TheseusLog.Information("Made the worktree {Folder} of {ClonePath} on {Branch}", step.Folder, step.ClonePath, branch);
            }

            ready.Add(new PreparedRepo(step.Repo, step.ClonePath, step.Folder,
                step.Kind == WorktreeKind.InPlace ? GitBranchReader.Read(step.ClonePath) : branch, step.Kind));
        }

        return new WorktreePreparation(ready, branch, null);
    }

    private (string? Refusal, Step? Step) Check(RowRepo repo, string branch, CloneLocator locator, WorktreeRequest request)
    {
        var match = locator.Find(repo.Reference);

        if (match.ClonePath is not { } clone)
        {
            return (match.Candidates.Count == 0
                ? $"No clone of {repo.Entry} was found in {locator.ClonesFolder}. Theseus does not clone: clone it there, or correct the row's Repos."
                : $"{repo.Entry} matches {match.Candidates.Count} clones, so Theseus cannot tell which to use: {string.Join(", ", match.Candidates)}. Write the row's repo in full, as <host>/<owner>/<repo>.", null);
        }

        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(clone));

        if (repo.IsWorkedInPlace)
        {
            return (null, new Step(name, clone, clone, WorktreeKind.InPlace, null));
        }

        var folder = ListedWorktree.Normal(Path.Combine(request.WorktreesFolder!, request.Slug, name));
        var listing = _git.Run(clone, "worktree", "list", "--porcelain");

        if (!listing.Succeeded)
        {
            return ($"The worktrees of {clone} could not be listed: {listing.FirstErrorLine}", null);
        }

        var listed = ListedWorktree.Parse(listing.Output);

        if (listed.FirstOrDefault(each => each.Is(folder)) is { } there)
        {
            return string.Equals(there.Branch, branch, StringComparison.Ordinal)
                ? (null, new Step(name, clone, folder, WorktreeKind.Adopted, null))
                : ($"{folder} is on {there.Branch ?? "a detached head"}, not {branch}. Put it on {branch}, or correct the row's Branch.", null);
        }

        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return (GitRepository.IsRoot(folder)
                ? $"{folder} is already a repository, but not a worktree of {clone}."
                : $"{folder} is not empty and is not a worktree. Move what is in it, then start again.", null);
        }

        if (listed.FirstOrDefault(each => string.Equals(each.Branch, branch, StringComparison.Ordinal)) is { } elsewhere)
        {
            return ($"{branch} is already checked out at {elsewhere.Folder}, so it cannot also be at {folder}.", null);
        }

        if (Exists(clone, $"refs/heads/{branch}"))
        {
            return (null, new Step(name, clone, folder, WorktreeKind.Made, ["worktree", "add", folder, branch]));
        }

        if (Exists(clone, $"refs/remotes/origin/{branch}"))
        {
            return (null, new Step(name, clone, folder, WorktreeKind.Made, ["worktree", "add", "--track", "-b", branch, folder, $"origin/{branch}"]));
        }

        return Base(clone, request.PrimaryBranch) is { } start
            ? (null, new Step(name, clone, folder, WorktreeKind.Made, ["worktree", "add", "--no-track", "-b", branch, folder, start]))
            : ($"{clone} has neither the primary branch {request.PrimaryBranch} nor a trunk (origin/HEAD, main or master) to branch {branch} from.", null);
    }

    /// <summary>
    /// What a new branch is made from (AC 10): the project's primary branch where the clone has it, otherwise the clone's
    /// trunk. A project whose primary branch carries no code, as Bug Fixing's does, so branches off each repo's trunk.
    /// </summary>
    private string? Base(string clone, string? primary)
    {
        if (ProjectBranchRules.HasPrimary(primary))
        {
            var name = primary!.Trim();

            if (Exists(clone, $"refs/remotes/origin/{name}"))
            {
                return $"refs/remotes/origin/{name}";
            }

            if (Exists(clone, $"refs/heads/{name}"))
            {
                return $"refs/heads/{name}";
            }
        }

        var head = _git.Run(clone, "symbolic-ref", "--quiet", "refs/remotes/origin/HEAD");

        if (head.Succeeded && head.Output.Trim() is { Length: > 0 } trunk)
        {
            return trunk;
        }

        return new[] { "refs/remotes/origin/main", "refs/remotes/origin/master", "refs/heads/main", "refs/heads/master" }
            .FirstOrDefault(reference => Exists(clone, reference));
    }

    private bool Exists(string clone, string reference) =>
        _git.Run(clone, "rev-parse", "--verify", "--quiet", $"{reference}^{{commit}}").Succeeded;

    private sealed record Step(string Repo, string ClonePath, string Folder, WorktreeKind Kind, string[]? Add);
}
