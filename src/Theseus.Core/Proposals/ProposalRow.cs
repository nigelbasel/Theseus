using Theseus.Core.Projects;

namespace Theseus.Core.Proposals;

/// <summary>
/// The workstreams row Theseus writes when the owner approves a proposal from a card in a project (1.1e AC 31): under the
/// objective the proposal names, with its records folder name, branch and repos; none named, the branch is
/// <see cref="NoBranch"/> and the repos <see cref="ReadOnlyRepos"/>, never invented.
/// </summary>
public static class ProposalRow
{
    /// <summary>The branch of a row whose proposal named none.</summary>
    public const string NoBranch = "none";

    /// <summary>The repos of a row whose proposal named none.</summary>
    public const string ReadOnlyRepos = "read-only";

    /// <summary>
    /// Why a proposal from a card in a project cannot be approved yet, or null when it can: it must name an objective of
    /// the project's file, and a records folder name that is a slug.
    /// </summary>
    public static string? WhyNot(ProjectFile file, ProposalRequest request)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Objective))
        {
            return "It names no objective: a workstream in a project serves one. The session proposes it again, naming it.";
        }

        if (ObjectiveOf(file, request.Objective) is null)
        {
            return $"It names objective {request.Objective}, which the project file does not have.";
        }

        if (request.Slug is { } slug && file.Objectives.SelectMany(objective => objective.Workstreams)
                .Any(row => string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase)))
        {
            return $"Its records folder, {slug}, is already in the project file's workstreams.";
        }

        return null;
    }

    /// <summary>The objective's number and the row for the new card, or null when <see cref="WhyNot"/> has a reason.</summary>
    public static (string Objective, ProjectWorkstreamRow Row)? For(ProjectFile file, ProposalRequest request, string cardKey)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(cardKey);

        if (WhyNot(file, request) is not null || ObjectiveOf(file, request.Objective!) is not { } objective)
        {
            return null;
        }

        var slug = request.Slug is { } named && ProjectMembership.IsSlug(named) ? named
            : ProjectMembership.IsSlug(cardKey) ? cardKey
            : null;

        // A records folder taken already, named or not, is never written twice (review of PR 6, finding 13).
        if (slug is null || file.Workstreams.Any(row => string.Equals(row.Slug, slug, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var branch = string.IsNullOrWhiteSpace(request.Branch) ? NoBranch : request.Branch.Trim();
        var repos = request.Repos.Count == 0 ? ReadOnlyRepos : string.Join(", ", request.Repos);

        return (objective.Number, new ProjectWorkstreamRow(cardKey, slug, branch, repos, 0));
    }

    /// <summary>
    /// A verifier's row (1.1e AC 31): its records folder verify-&lt;slug&gt;, numbered when taken, branch none and repos
    /// read-only.
    /// </summary>
    public static ProjectWorkstreamRow ForVerifier(ProjectFile file, string checkedSlug, string verifierKey)
    {
        ArgumentNullException.ThrowIfNull(file);
        var taken = file.Workstreams.Select(row => row.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = $"verify-{checkedSlug}";

        for (var number = 2; taken.Contains(name); number++)
        {
            name = $"verify-{checkedSlug}-{number.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }

        return new ProjectWorkstreamRow(verifierKey, name, NoBranch, ReadOnlyRepos, 0);
    }

    // By its number ("1.2"), or by its title.
    private static ProjectObjective? ObjectiveOf(ProjectFile file, string named)
    {
        var wanted = named.Trim();

        return file.Objectives.FirstOrDefault(objective => string.Equals(objective.Number, wanted, StringComparison.OrdinalIgnoreCase))
               ?? file.Objectives.FirstOrDefault(objective => string.Equals(objective.Title, wanted, StringComparison.OrdinalIgnoreCase));
    }
}
