using Theseus.Core.Domain;

namespace Theseus.Core.Worktrees;

/// <summary>
/// Finds a repo's clone by the layout rule (worktree-per-repo AC 5 to 7): a repository's remote gives its clone's place
/// under the clones folder, <c>&lt;clones&gt;\&lt;host&gt;\&lt;owner&gt;\&lt;repo&gt;</c>. It only finds: it never clones.
/// </summary>
public sealed class CloneLocator
{
    private const int Depth = 4;

    private readonly string _clonesFolder;
    private readonly IGitRemoteReader _remotes;
    private List<string>? _clones;

    /// <summary>Creates a locator over one clones folder.</summary>
    public CloneLocator(string clonesFolder, IGitRemoteReader? remotes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clonesFolder);
        _clonesFolder = Path.GetFullPath(clonesFolder);
        _remotes = remotes ?? new GitRemoteReader();
    }

    /// <summary>The clones folder searched, for a notice.</summary>
    public string ClonesFolder => _clonesFolder;

    /// <summary>The clone a reference names, or the candidates when not exactly one.</summary>
    public CloneMatch Find(string reference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        var segments = RepoEntries.Segments(reference);

        if (segments.Count > 1)
        {
            var direct = Path.Combine([_clonesFolder, .. segments]);

            if (IsClone(direct))
            {
                return new CloneMatch(Path.GetFullPath(direct), [Path.GetFullPath(direct)]);
            }
        }

        // The host is the one segment written differently (github.com in a URL, github in the layout), so it is not compared.
        var tail = segments.Count > 1 ? segments.Skip(1).ToList() : [.. segments];
        var found = Clones().Where(clone => EndsWith(RelativeSegments(clone), tail) || EndsWith(RemoteSegments(clone), tail)).ToList();

        return new CloneMatch(found.Count == 1 ? found[0] : null, found);
    }

    private static bool IsClone(string folder) => Directory.Exists(Path.Combine(folder, ".git"));

    private static bool EndsWith(IReadOnlyList<string> whole, IReadOnlyList<string> tail) =>
        whole.Count >= tail.Count
        && whole.Skip(whole.Count - tail.Count).Zip(tail).All(pair => string.Equals(pair.First, pair.Second, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> RelativeSegments(string clone) => RepoEntries.Segments(Path.GetRelativePath(_clonesFolder, clone));

    private IReadOnlyList<string> RemoteSegments(string clone)
    {
        if (_remotes.ReadOriginUrl(clone) is not { } url)
        {
            return [];
        }

        var path = url.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Uri.UnescapeDataString(uri.AbsolutePath)
            : url[(url.IndexOf(':', StringComparison.Ordinal) + 1)..];

        return [.. RepoEntries.Segments(path)
            .Where(segment => !string.Equals(segment, "_git", StringComparison.OrdinalIgnoreCase))
            .Select(segment => segment.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? segment[..^4] : segment)];
    }

    private List<string> Clones()
    {
        if (_clones is not null)
        {
            return _clones;
        }

        _clones = [];
        Walk(_clonesFolder, 0);

        return _clones;
    }

    private void Walk(string folder, int depth)
    {
        if (depth >= Depth)
        {
            return;
        }

        IEnumerable<string> children;

        try
        {
            children = [.. Directory.EnumerateDirectories(folder)];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var child in children)
        {
            if (IsClone(child))
            {
                _clones!.Add(Path.GetFullPath(child));
            }
            else if (!File.Exists(Path.Combine(child, ".git")))
            {
                Walk(child, depth + 1);
            }
        }
    }
}
