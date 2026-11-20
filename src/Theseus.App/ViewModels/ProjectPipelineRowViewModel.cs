namespace Theseus.App.ViewModels;

/// <summary>
/// One row of the project view's pipeline: an objective that has workstreams, across the seven stage columns; or a
/// group of cards that sit at no stage (No slug yet, Not in the project file), whose cells are empty (AC 4, 15).
/// </summary>
/// <param name="Heading">The objective, such as "1.1 Checks shown on cards", or the group's name.</param>
/// <param name="Cells">One per stage column, in order; empty for a group.</param>
/// <param name="Loose">A group's cards; empty for an objective.</param>
public sealed record ProjectPipelineRowViewModel(
    string Heading,
    IReadOnlyList<ProjectStageCellViewModel> Cells,
    IReadOnlyList<ProjectEntryViewModel> Loose)
{
    /// <summary>Whether it is a group of cards at no stage.</summary>
    public bool IsGroup => Cells.Count == 0;
}
