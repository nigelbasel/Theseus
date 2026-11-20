using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One stage of one objective's row in the project view (AC 6, 15).</summary>
/// <param name="Stage">The column.</param>
/// <param name="Entries">The slugs that sit at this stage.</param>
public sealed record ProjectStageCellViewModel(PipelineStage Stage, IReadOnlyList<ProjectEntryViewModel> Entries);
