namespace Theseus.Core.Projects;

/// <summary>One row of an objective's workstreams table: a workstream working under one slug.</summary>
/// <param name="Workstream">A plain name for the workstream, never a card id (P19). Rows sharing it are one workstream's slugs.</param>
/// <param name="Slug">Names its records folder in the project home, <c>work\&lt;slug&gt;\</c>, and its branch.</param>
/// <param name="Branch">The branch its code is on until it is merged into the primary.</param>
/// <param name="Repos">Every repo it changes, as written.</param>
/// <param name="Line">Its line in the project file, from 1.</param>
public sealed record ProjectWorkstreamRow(string Workstream, string Slug, string Branch, string Repos, int Line)
{
    /// <summary>
    /// Its optional What cell: a few words saying what the piece achieves, the title its box shows (1.1f A4, B7); null
    /// when the table has no What column or the cell is empty.
    /// </summary>
    public string? What { get; init; }
}
