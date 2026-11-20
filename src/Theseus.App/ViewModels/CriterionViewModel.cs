using System.Windows.Input;

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
    /// state the file does not hold (1.1c review, item 7).
    /// </summary>
    public string TickGlyph => IsTicked ? "☑" : "☐";

    /// <summary>What its button does.</summary>
    public string ToolTip => IsTicked ? "Untick this criterion in project.md" : "Tick this criterion in project.md";
}
