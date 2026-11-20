using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// A project's cards laid out by objective and slug, for the project view (spec AC 4, 8, 10 and 11). Archived cards are
/// left out; a verifier sits beside its subject in verify until it ends, then leaves the row.
/// </summary>
/// <param name="File">The project file.</param>
/// <param name="Objectives">Every objective, with its slugs.</param>
/// <param name="NoSlugYet">Cards in the project with no slug yet.</param>
/// <param name="NotInProjectFile">Cards whose slug the project file does not list.</param>
public sealed record ProjectLayout(
    ProjectFile File,
    IReadOnlyList<ObjectiveLayout> Objectives,
    IReadOnlyList<string> NoSlugYet,
    IReadOnlyList<string> NotInProjectFile)
{
    /// <summary>
    /// The primary's pull request into the trunk: passed when merged, read through objective 1.2's status, which is not
    /// built yet (AC 7). There is no Accept for it.
    /// </summary>
    public GateState MainPullRequest => GateState.NotReadYet;

    /// <summary>Lays out a project's cards.</summary>
    /// <param name="file">The project file.</param>
    /// <param name="cards">Every card on the board; those of other projects and ad hoc ones are left out.</param>
    /// <param name="stageOf">Where a slug's work stands, from <see cref="StageCalculator"/>.</param>
    /// <param name="hasEnded">Whether a verifier card's job is over.</param>
    public static ProjectLayout Build(ProjectFile file, IReadOnlyList<Workstream> cards,
        Func<ProjectObjective, ProjectWorkstreamRow, WorkstreamStage> stageOf, Func<Workstream, bool> hasEnded)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(stageOf);
        ArgumentNullException.ThrowIfNull(hasEnded);

        var members = cards
            .Where(card => !card.IsArchived && string.Equals(card.Project, file.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var listed = file.Workstreams.Select(row => row.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> Holders(string slug, bool verifiers) =>
        [
            .. members
                .Where(card => (card.Role == WorkstreamRole.Verifier) == verifiers
                               && string.Equals(card.Slug, slug, StringComparison.OrdinalIgnoreCase)
                               && !(verifiers && hasEnded(card)))
                .Select(card => card.Key),
        ];

        var withCards = file.Workstreams
            .Where(row => Holders(row.Slug, verifiers: false).Count > 0)
            .Select(row => row.Workstream)
            .ToHashSet(StringComparer.Ordinal);

        var objectives = file.Objectives.Select(objective => new ObjectiveLayout(objective, [
            .. objective.Workstreams.Select(row =>
            {
                var holders = Holders(row.Slug, verifiers: false);

                return new SlugEntry(row, stageOf(objective, row), holders, Holders(row.Slug, verifiers: true),
                    holders.Count == 0 && withCards.Contains(row.Workstream));
            }),
        ])).ToList();

        var noSlug = members.Where(card => string.IsNullOrWhiteSpace(card.Slug)).Select(card => card.Key).ToList();
        var unlisted = members
            .Where(card => !string.IsNullOrWhiteSpace(card.Slug) && !listed.Contains(card.Slug!))
            .Where(card => !(card.Role == WorkstreamRole.Verifier && hasEnded(card)))
            .Select(card => card.Key)
            .ToList();

        return new ProjectLayout(file, objectives, noSlug, unlisted);
    }
}
