using System.Text.RegularExpressions;

namespace Theseus.Core.Projects;

/// <summary>
/// The reads a project needs from one repository folder, each one git command. Branches are local ones: Theseus reads
/// what is on the machine and fetches nothing (spec AC 2).
/// </summary>
public sealed partial class GitRepositoryReader
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

    /// <summary>Why the folder cannot be read as a repository, in git's own words, or null when it can.</summary>
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
            { Succeeded: false } => string.Join(" ", probe.Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                    is { Length: > 0 } said ? said : "git refused the folder",
            _ => null,
        };
    }

    /// <summary>
    /// The repository's default branch: the one its origin's HEAD names, else a local <c>main</c> or <c>master</c>.
    /// The local branch when there is one, else origin's copy of it as a full ref, which is also on the machine.
    /// </summary>
    public string? DefaultBranch()
    {
        if (Line(Run("symbolic-ref", "--quiet", "refs/remotes/origin/HEAD")) is { } remoteHead)
        {
            var name = remoteHead.StartsWith("refs/remotes/origin/", StringComparison.Ordinal)
                ? remoteHead["refs/remotes/origin/".Length..]
                : remoteHead;

            return HasBranch(name) ? name : remoteHead;
        }

        return new[] { "main", "master" }.FirstOrDefault(HasBranch);
    }

    /// <summary>The branch the folder has checked out, or null when detached.</summary>
    public string? CurrentBranch() => Line(Run("symbolic-ref", "--quiet", "--short", "HEAD"));

    /// <summary>Whether a local branch of this name exists.</summary>
    public bool HasBranch(string branch) => Run("rev-parse", "--verify", "--quiet", $"refs/heads/{branch}").Succeeded;

    /// <summary>The commit a branch or commit names, or null.</summary>
    public string? Commit(string revision) => Line(Run("rev-parse", "--verify", "--quiet", $"{Ref(revision)}^{{commit}}"));

    /// <summary>A file's text at a branch or commit, or null when it is not there.</summary>
    public string? Text(string revision, string path)
    {
        var output = Run("cat-file", "blob", $"{Ref(revision)}:{path}");

        return output.Succeeded ? output.Output : null;
    }

    /// <summary>The files (not folders) directly inside a folder at a branch or commit; empty when it is not there.</summary>
    public IReadOnlyList<string> Names(string revision, string directory)
    {
        var prefix = directory.TrimEnd('/') + "/";
        var output = Run("ls-tree", Ref(revision), "--", prefix);

        return
        [
            .. Lines(output)
                .Select(line => TreeEntry().Match(line))
                .Where(entry => entry.Success && entry.Groups["type"].Value == "blob")
                .Select(entry => entry.Groups["path"].Value)
                .Select(path => path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path),
        ];
    }

    /// <summary>Every local branch and the commit at its tip, in one command; empty when git could not say.</summary>
    public IReadOnlyDictionary<string, string> BranchTips()
    {
        var tips = new Dictionary<string, string>(StringComparer.Ordinal);

        // The full name: with a tag of the same name, the short form comes out as heads/<name>.
        foreach (var line in Lines(Run("for-each-ref", "refs/heads", "--format=%(objectname) %(refname)")))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);

            if (space > 0 && line[(space + 1)..] is var name && name.StartsWith("refs/heads/", StringComparison.Ordinal))
            {
                tips[name["refs/heads/".Length..]] = line[..space];
            }
        }

        return tips;
    }

    /// <summary>The folders directly inside a folder at a branch or commit, each with its tree id, in one command.</summary>
    public IReadOnlyDictionary<string, string> Folders(string revision, string directory)
    {
        var prefix = directory.TrimEnd('/') + "/";
        var folders = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in Lines(Run("ls-tree", Ref(revision), "--", prefix)).Select(line => TreeEntry().Match(line)))
        {
            if (entry.Success && entry.Groups["type"].Value == "tree")
            {
                var path = entry.Groups["path"].Value;
                folders[path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..] : path] = entry.Groups["id"].Value;
            }
        }

        return folders;
    }

    /// <summary>The last commit at or before a branch or commit that changed a path, or null.</summary>
    public string? LastCommit(string revision, string path) => Line(Run("log", "-1", "--format=%H", Ref(revision), "--", path));

    /// <summary>Whether one branch or commit is an ancestor of (or the same as) another; null when git could not say.</summary>
    public bool? IsAncestor(string ancestor, string descendant)
    {
        var output = Run("merge-base", "--is-ancestor", Ref(ancestor), Ref(descendant));

        return output.Ran && output.ExitCode is 0 or 1 ? output.ExitCode == 0 : null;
    }

    /// <summary>
    /// The oldest commit on a branch's first-parent line that added or removed <paramref name="text"/> in a path: for a
    /// change merged in, the merge, so its first parent is the branch as it stood before the change arrived.
    /// </summary>
    public string? FirstCommitChanging(string revision, string path, string text) =>
        Lines(Run("log", "--first-parent", "--diff-merges=first-parent", "--reverse", "--format=%H", $"-S{text}", Ref(revision),
            "--", path)).FirstOrDefault();

    /// <summary>A commit's first parent, or null for a root commit.</summary>
    public string? Parent(string commit) => Commit($"{commit}^");

    /// <summary>
    /// A branch name as its full ref, so a tag of the same name cannot win and a name from a table can never reach git as
    /// an option. A commit id, a full ref, or a revision already built from one (such as a parent) is left alone.
    /// </summary>
    public static string Ref(string revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return revision.StartsWith("refs/", StringComparison.Ordinal) || CommitId().IsMatch(revision)
            ? revision
            : $"refs/heads/{revision}";
    }

    private GitOutput Run(params string[] arguments) => _git.Run(Folder, arguments);

    private static string? Line(GitOutput output) =>
        output.Succeeded && output.Output.Trim() is { Length: > 0 } line ? line : null;

    private static IEnumerable<string> Lines(GitOutput output) =>
        output.Succeeded
            ? output.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

    [GeneratedRegex(@"^[0-9a-f]{7,64}(\^.*)?$")]
    private static partial Regex CommitId();

    [GeneratedRegex(@"^\d+ (?<type>\w+) (?<id>[0-9a-f]+)\t(?<path>.+)$")]
    private static partial Regex TreeEntry();
}
