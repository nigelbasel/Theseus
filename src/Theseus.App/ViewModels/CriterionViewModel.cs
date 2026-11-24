using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One criterion of an objective in the Properties pane, ticked in the project file (1.1c AC 8; 1.1d AC 34).
/// </summary>
/// <param name="Text">The criterion.</param>
/// <param name="IsTicked">Whether the file has it ticked.</param>
/// <param name="ToggleCommand">Ticks or unticks it in the file, or null when the view cannot edit.</param>
public sealed record CriterionViewModel(string Text, bool IsTicked, ICommand? ToggleCommand)
{
    /// <summary>
    /// The criterion's box as the file has it. A glyph on a button, not a check box, so a refused tick never shows a
    /// state the file does not hold (1.1c review, item 7). Drawn in the icon font: E73A ticked, E739 not (baseline W25).
    /// </summary>
    public string TickGlyph => IsTicked ? "" : "";

    /// <summary>On whom an unticked criterion waits, from its Waiting line: "Waiting for you: …"; empty otherwise (item 6).</summary>
    public string Waiting { get; init; } = string.Empty;

    /// <summary>Whether it says on whom it waits.</summary>
    public bool HasWaiting => Waiting.Length > 0;

    /// <summary>What a screen reader says for its tick: whether it is met, and the criterion.</summary>
    public string AccessibleName => $"{(IsTicked ? "Met" : "Not met")}: {Text}";

    /// <summary>What its button does.</summary>
    public string ToolTip => IsTicked ? "Untick this criterion in project.md" : "Tick this criterion in project.md";

    /// <summary>An objective's criteria, each ticked or unticked in the file through the view's edits when it has them.</summary>
    internal static IReadOnlyList<CriterionViewModel> Of(ProjectObjective objective, ProjectViewEdits? edits) =>
    [
        .. objective.Criteria.Select(criterion => new CriterionViewModel(criterion.Text, criterion.IsTicked,
            edits is null ? null : new RelayCommand(_ => edits.TickCriterion(objective, criterion, !criterion.IsTicked)))
        {
            Waiting = criterion.IsTicked || criterion.Waiting is not { } waiting
                ? string.Empty
                : waiting.IsOwner ? $"Waiting for you: {waiting.What}" : $"Waiting on work: {waiting.What}",
        }),
    ];
}
