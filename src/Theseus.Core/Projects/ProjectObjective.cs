namespace Theseus.Core.Projects;

/// <summary>A derived objective: a <c>### Objective n.m: title</c> heading, its criteria, pipeline and workstreams.</summary>
/// <param name="Number">Its number, such as <c>1.1</c>.</param>
/// <param name="Title">Its title.</param>
/// <param name="Criteria">Its acceptance criteria, in order.</param>
/// <param name="Pipelines">
/// The pipelines its workstreams run. Empty while the file says <c>[to be chosen]</c>; more than one when its workstreams
/// run different pipelines, each workstream's then read from its artefacts.
/// </param>
/// <param name="Workstreams">Its workstreams table's rows.</param>
/// <param name="Line">The heading's line, from 1.</param>
public sealed record ProjectObjective(
    string Number,
    string Title,
    IReadOnlyList<ProjectCriterion> Criteria,
    IReadOnlyList<PipelineKind> Pipelines,
    IReadOnlyList<ProjectWorkstreamRow> Workstreams,
    int Line)
{
    /// <summary>Done when every criterion is ticked; an objective whose criteria are not written yet is not done (AC 11).</summary>
    public bool IsDone => Criteria.Count > 0 && Criteria.All(criterion => criterion.IsTicked);

    /// <summary>How many of its criteria are ticked.</summary>
    public int TickedCount => Criteria.Count(criterion => criterion.IsTicked);
}
