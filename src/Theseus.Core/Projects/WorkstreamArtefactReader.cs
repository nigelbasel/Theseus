namespace Theseus.Core.Projects;

/// <summary>
/// Reads a slug's <c>work/&lt;slug&gt;/</c> with git (spec AC 5): from the workstream's own branch (the table's Branch)
/// while it is not merged into the primary, from the primary once it is. When the branch is not on the machine the
/// primary is read; when the primary has nothing for the slug either, it is not on this machine.
/// </summary>
public static class WorkstreamArtefactReader
{
    /// <summary>
    /// The commit before the project moved to method-v0.2 on its primary: the parent of the oldest commit that wrote the
    /// <c>Kit: method-v0.2</c> line into its file. Null when there is none (the file was born at v0.2) or it cannot be read.
    /// </summary>
    public static string? KitMoveParent(GitRepositoryReader repository, ProjectFile project)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(project);

        var move = repository.FirstCommitChanging(project.PrimaryBranch, $"{ProjectCatalog.ProjectsFolder}/{project.Name}.md",
            MethodKits.CurrentKitLine);

        return move is null ? null : repository.Parent(move);
    }

    /// <summary>Reads one slug's artefacts.</summary>
    /// <param name="repository">The project's home repository.</param>
    /// <param name="project">The project, read from its primary.</param>
    /// <param name="row">The slug's row in the project file.</param>
    /// <param name="kitMoveParent">From <see cref="KitMoveParent"/>, read once per project.</param>
    public static WorkstreamArtefacts Read(GitRepositoryReader repository, ProjectFile project, ProjectWorkstreamRow row, string? kitMoveParent)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(row);

        var source = Source(repository, project.PrimaryBranch, row.Branch, $"work/{row.Slug}");

        return ReadFrom(repository, row.Slug, source, kitMoveParent);
    }

    /// <summary>Reads one slug's artefacts from a source already chosen, as <see cref="ArtefactCache"/> does.</summary>
    public static WorkstreamArtefacts ReadFrom(GitRepositoryReader repository, string slug, WorkstreamSource source, string? kitMoveParent)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(source);

        var folder = $"work/{slug}";

        if (source.Branch is null)
        {
            return WorkstreamArtefacts.NotOnMachine(slug);
        }

        // Read at the commit the source names, so its files, its tree and its commit always agree, however the
        // branch moves meanwhile (PR 2 re-review, item 4).
        var branch = source.Commit ?? source.Branch;
        var files = repository.Names(branch, folder).ToHashSet(StringComparer.Ordinal);

        string? Text(string name) => files.Contains(name) ? repository.Text(branch, $"{folder}/{name}") : null;

        var spec = Text("spec.md");
        var fix = Text("fix.md");
        var rule = BugArtefacts.RuleText(fix);

        return new WorkstreamArtefacts(slug, source, files)
        {
            FixText = fix,
            SpecText = spec,
            EvidenceText = Text("evidence.md"),
            SpecHash = spec is null ? null : SpecIdentity.Hash(spec),
            SpecCommit = spec is null ? null : repository.LastCommit(branch, $"{folder}/spec.md"),
            RuleHash = rule is null ? null : SpecIdentity.Hash(rule),
            RuleCommit = rule is null ? null : repository.LastCommit(branch, $"{folder}/fix.md"),
            IsBeforeKit = source.IsMerged && IsBeforeKit(repository, branch, folder, kitMoveParent),
        };
    }

    private static WorkstreamSource Source(GitRepositoryReader repository, string primary, string branch, string folder)
    {
        if (!string.Equals(branch, primary, StringComparison.Ordinal) && repository.HasBranch(branch)
            && repository.IsAncestor(branch, primary) != true)
        {
            return new WorkstreamSource(branch, repository.Commit(branch), false);
        }

        var branchHere = string.Equals(branch, primary, StringComparison.Ordinal) || repository.HasBranch(branch);

        // A branch deleted after its merge leaves its work on the primary, so only a slug the primary lacks as well
        // counts as not on this machine.
        if (!branchHere && repository.Names(primary, folder).Count == 0)
        {
            return WorkstreamSource.NotOnMachine;
        }

        return new WorkstreamSource(primary, repository.Commit(primary), true);
    }

    // Work from before the move is work none of whose artefacts were written since: its last commit is already in the
    // primary as the move found it. Work merged after the move, or still being written, carries gate markers.
    private static bool IsBeforeKit(GitRepositoryReader repository, string branch, string folder, string? kitMoveParent)
    {
        if (kitMoveParent is null || repository.LastCommit(branch, folder) is not { } lastWork)
        {
            return false;
        }

        return repository.IsAncestor(lastWork, kitMoveParent) == true;
    }
}
