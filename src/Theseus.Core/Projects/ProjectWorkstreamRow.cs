namespace Theseus.Core.Projects;

/// <summary>One row of an objective's workstreams table: a workstream working under one slug.</summary>
/// <param name="Workstream">A plain name for the workstream, never a card id (P19). Rows sharing it are one workstream's slugs.</param>
/// <param name="Slug">Names its artefacts folder, <c>work/&lt;slug&gt;/</c>, and its branch.</param>
/// <param name="Branch">The branch its work is on until it is merged into the primary.</param>
/// <param name="Repos">Every repo it changes, as written.</param>
/// <param name="Line">Its line in the project file, from 1.</param>
public sealed record ProjectWorkstreamRow(string Workstream, string Slug, string Branch, string Repos, int Line);
