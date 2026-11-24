using Theseus.Core.Worktrees;

namespace Theseus.Tests.Worktrees;

/// <summary>
/// Real git repositories in a temporary folder, for the worktree tests: a bare origin per repo, and its clone at
/// <c>&lt;root&gt;\clones\github\owner\&lt;repo&gt;</c> with a commit on main. Git runs with no user hooks or signing.
/// </summary>
internal sealed class ScratchRepos : IDisposable
{
    public ScratchRepos()
    {
        Root = Path.Combine(Path.GetTempPath(), $"theseus-worktrees-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Root);
        Clones = Path.Combine(Root, "clones");
        Worktrees = Path.Combine(Root, "work");
        Directory.CreateDirectory(Clones);
    }

    public string Root { get; }

    public string Clones { get; }

    public string Worktrees { get; }

    public IGit Git { get; } = new GitProcess();

    /// <summary>Makes a repo with one commit on main, pushed to its origin, and returns its clone.</summary>
    public string Repo(string name)
    {
        var origin = Path.Combine(Root, "origins", $"{name}.git");
        Directory.CreateDirectory(origin);
        Must(origin, "init", "--bare", "-b", "main");

        var clone = Path.Combine(Clones, "github", "owner", name);
        Directory.CreateDirectory(Path.GetDirectoryName(clone)!);
        Must(Root, "clone", origin, clone);
        File.WriteAllText(Path.Combine(clone, "readme.txt"), name);
        File.WriteAllLines(Path.Combine(clone, ".gitignore"), ["bin/", "*.local.json"]);
        Commit(clone, "first");
        Must(clone, "push", "origin", "HEAD:main");
        Must(clone, "remote", "set-head", "origin", "main");

        return Path.GetFullPath(clone);
    }

    /// <summary>Pushes a new branch off main to the origin, leaving no local branch.</summary>
    public void RemoteBranch(string clone, string branch)
    {
        Must(clone, "push", "origin", $"main:refs/heads/{branch}");
        Must(clone, "fetch", "origin");
    }

    public void Commit(string folder, string message)
    {
        Must(folder, "add", "-A");
        Must(folder, "-c", "user.name=Theseus", "-c", "user.email=theseus@localhost", "-c", "commit.gpgsign=false",
            "-c", "core.hooksPath=", "commit", "-m", message);
    }

    public GitResult Must(string folder, params string[] arguments)
    {
        var result = Git.Run(folder, ["-c", "core.hooksPath=", .. arguments]);

        Assert.That(result.Succeeded, Is.True, $"git {string.Join(' ', arguments)} in {folder}: {result.Error}");

        return result;
    }

    public string? Upstream(string folder, string branch)
    {
        var result = Git.Run(folder, "rev-parse", "--abbrev-ref", $"{branch}@{{upstream}}");

        return result.Succeeded ? result.Output.Trim() : null;
    }

    public bool BranchExists(string clone, string branch) =>
        Git.Run(clone, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}").Succeeded;

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        // Git writes its objects read-only.
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(Root, recursive: true);
    }
}
