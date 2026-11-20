namespace Theseus.Core.Projects;

/// <summary>
/// Re-reads a slug's files only when what they are read from has moved (PR 2 review, open cost). Each read starts with
/// every branch tip in one command; with no tip moved that is the only command run. Otherwise:
/// <list type="bullet">
/// <item>whether a workstream branch is merged is worked out again only when its tip or the primary's moves;</item>
/// <item>a slug is read from the source that rule gives, so a merge moves it to the primary;</item>
/// <item>its files are read again only when that source's <c>work/&lt;slug&gt;/</c> tree, the source itself or the move
/// to method-v0.2 differs from the last read. Otherwise the last artefacts are kept, with the source's new tip.</item>
/// </list>
/// The spec's identity (for acceptance) and "before method-v0.2" come from those artefacts, so they follow the same keys.
/// One instance lives as long as the board; reads run one at a time.
/// </summary>
public sealed class ArtefactCache
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _folders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _merged = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _kitMoves = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Key, WorkstreamArtefacts Artefacts)> _artefacts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Every listed slug's artefacts in a readable project, keyed by slug.</summary>
    public IReadOnlyDictionary<string, WorkstreamArtefacts> Read(GitRepositoryReader repository, ProjectFile project)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(project);

        lock (_gate)
        {
            var tips = repository.BranchTips();
            var primary = project.PrimaryBranch;
            var result = new Dictionary<string, WorkstreamArtefacts>(StringComparer.OrdinalIgnoreCase);

            if (!tips.TryGetValue(primary, out var primaryTip))
            {
                foreach (var row in project.Workstreams)
                {
                    result[row.Slug] = WorkstreamArtefacts.NotOnMachine(row.Slug);
                }

                return result;
            }

            var kitMoveParent = KitMove(repository, project, primaryTip);

            foreach (var row in project.Workstreams)
            {
                result[row.Slug] = ReadSlug(repository, project, row, tips, primaryTip, kitMoveParent);
            }

            return result;
        }
    }

    private WorkstreamArtefacts ReadSlug(GitRepositoryReader repository, ProjectFile project, ProjectWorkstreamRow row,
        IReadOnlyDictionary<string, string> tips, string primaryTip, string? kitMoveParent)
    {
        var primary = project.PrimaryBranch;
        var source = Source(repository, primary, primaryTip, row, tips);

        if (source.Branch is not { } branch)
        {
            return WorkstreamArtefacts.NotOnMachine(row.Slug);
        }

        var tree = Folders(repository, branch, source.Commit!).GetValueOrDefault(row.Slug) ?? "none";
        var key = string.Join("|", branch, tree, source.IsMerged, source.IsMerged ? kitMoveParent ?? "none" : "-");
        var slugKey = $"{repository.Folder}|{project.Name}|{row.Slug}";

        if (_artefacts.TryGetValue(slugKey, out var cached) && cached.Key == key)
        {
            return cached.Artefacts with { Source = source };
        }

        var artefacts = WorkstreamArtefactReader.ReadFrom(repository, row.Slug, source, kitMoveParent);
        _artefacts[slugKey] = (key, artefacts);

        return artefacts;
    }

    // The same rule as WorkstreamArtefactReader's: the workstream's branch while unmerged, else the primary, else
    // nowhere when the primary has no folder for the slug either.
    private WorkstreamSource Source(GitRepositoryReader repository, string primary, string primaryTip, ProjectWorkstreamRow row,
        IReadOnlyDictionary<string, string> tips)
    {
        if (!string.Equals(row.Branch, primary, StringComparison.Ordinal) && tips.TryGetValue(row.Branch, out var branchTip)
            && !Merged(repository, branchTip, primaryTip))
        {
            return new WorkstreamSource(row.Branch, branchTip, false);
        }

        var branchHere = string.Equals(row.Branch, primary, StringComparison.Ordinal) || tips.ContainsKey(row.Branch);

        if (!branchHere && !Folders(repository, primary, primaryTip).ContainsKey(row.Slug))
        {
            return WorkstreamSource.NotOnMachine;
        }

        return new WorkstreamSource(primary, primaryTip, true);
    }

    private bool Merged(GitRepositoryReader repository, string branchTip, string primaryTip)
    {
        var key = $"{repository.Folder}|{branchTip}|{primaryTip}";

        if (!_merged.TryGetValue(key, out var merged))
        {
            merged = repository.IsAncestor(branchTip, primaryTip) == true;
            _merged[key] = merged;
        }

        return merged;
    }

    private IReadOnlyDictionary<string, string> Folders(GitRepositoryReader repository, string branch, string tip)
    {
        var key = $"{repository.Folder}|{branch}|{tip}";

        if (!_folders.TryGetValue(key, out var folders))
        {
            folders = repository.Folders(tip, "work");
            _folders[key] = folders;
        }

        return folders;
    }

    private string? KitMove(GitRepositoryReader repository, ProjectFile project, string primaryTip)
    {
        var key = $"{repository.Folder}|{project.Name}|{primaryTip}";

        if (!_kitMoves.TryGetValue(key, out var parent))
        {
            parent = WorkstreamArtefactReader.KitMoveParent(repository, project);
            _kitMoves[key] = parent;
        }

        return parent;
    }
}
