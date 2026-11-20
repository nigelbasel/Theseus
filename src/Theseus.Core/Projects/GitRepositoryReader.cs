namespace Theseus.Core.Projects;

/// <summary>
/// The reads a project needs from one repository folder, each one git command. Branches are local ones: Theseus reads
/// what is on the machine and fetches nothing (spec AC 2).
/// </summary>
public sealed class GitRepositoryReader
{
    private readonly IGitReader _git;

    /// <summary>Creates a reader over a folder.</summary>
    public GitRepositoryReader(IGitReader git, string folder)
    {
        _git = git;
        Folder = folder;
    }

    /// <summary>The folder read.</summary>
    public string Folder { get; }

    /// <summary>Why the folder cannot be read as a repository, or null when it can.</summary>
    public string? Problem()
    {
        if (!Directory.Exists(Folder))
        {
            return "the folder does not exist";
        }

        var probe = Run("rev-parse", "--git-dir");

        return probe switch
        {
            { Ran: false } => probe.Error,
            { Succeeded: false } => "not a git repository",
            _ => null,
        };
    }

    /// <summary>
    /// The repository's default branch: the one its origin's HEAD names, else a local <c>main</c> or <c>master</c>.
    /// The local branch when there is one, else origin's copy of it, which is also on the machine.
    /// </summary>
    public string? DefaultBranch()
    {
        if (Line(Run("symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD")) is { } remoteHead)
        {
            var name = remoteHead.StartsWith("origin/", StringComparison.Ordinal) ? remoteHead["origin/".Length..] : remoteHead;

            return HasBranch(name) ? name : remoteHead;
        }

        return new[] { "main", "master" }.FirstOrDefault(HasBranch);
    }

    /// <summary>The branch the folder has checked out, or null when detached.</summary>
    public string? CurrentBranch() => Line(Run("symbolic-ref", "--quiet", "--short", "HEAD"));

    /// <summary>Whether a local branch of this name exists.</summary>
    public bool HasBranch(string branch) => Run("rev-parse", "--verify", "--quiet", $"refs/heads/{branch}").Succeeded;

    /// <summary>The commit a revision names, or null.</summary>
    public string? Commit(string revision) => Line(Run("rev-parse", "--verify", "--quiet", $"{revision}^{{commit}}"));

    /// <summary>A file's text at a revision, or null when it is not there.</summary>
    public string? Text(string revision, string path)
    {
        var output = Run("cat-file", "blob", $"{revision}:{path}");

        return output.Succeeded ? output.Output : null;
    }

    /// <summary>The names directly inside a folder at a revision; empty when it is not there.</summary>
    public IReadOnlyList<string> Names(string revision, string directory)
    {
        var prefix = directory.TrimEnd('/') + "/";
        var output = Run("ls-tree", "--name-only", revision, "--", prefix);

        return output.Succeeded
            ? [.. Lines(output).Select(line => line.StartsWith(prefix, StringComparison.Ordinal) ? line[prefix.Length..] : line)]
            : [];
    }

    /// <summary>The last commit at or before a revision that changed a path, or null.</summary>
    public string? LastCommit(string revision, string path) => Line(Run("log", "-1", "--format=%H", revision, "--", path));

    /// <summary>Whether one commit is an ancestor of (or the same as) another; null when git could not say.</summary>
    public bool? IsAncestor(string ancestor, string descendant)
    {
        var output = Run("merge-base", "--is-ancestor", ancestor, descendant);

        return output.Ran && output.ExitCode is 0 or 1 ? output.ExitCode == 0 : null;
    }

    /// <summary>The oldest commit at or before a revision that added or removed <paramref name="text"/> in a path.</summary>
    public string? FirstCommitChanging(string revision, string path, string text) =>
        Lines(Run("log", "--reverse", "--format=%H", $"-S{text}", revision, "--", path)).FirstOrDefault();

    /// <summary>A commit's first parent, or null for a root commit.</summary>
    public string? Parent(string commit) => Commit($"{commit}^");

    private GitOutput Run(params string[] arguments) => _git.Run(Folder, arguments);

    private static string? Line(GitOutput output) =>
        output.Succeeded && output.Output.Trim() is { Length: > 0 } line ? line : null;

    private static IEnumerable<string> Lines(GitOutput output) =>
        output.Succeeded
            ? output.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
}
