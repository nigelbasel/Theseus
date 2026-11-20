using System.Windows.Input;

namespace Theseus.App.ViewModels;

/// <summary>
/// One item of the project view's tree: a phase, an objective (criteria ticked n of m), one of its criteria, or a
/// workstream slug, with its passed gates and anything waiting on the owner marked (AC 15). Selecting it scrolls the
/// pipeline to its row. A criterion can be ticked, an objective edited, and a phase given a new objective (1.1c AC 8).
/// </summary>
/// <param name="Text">What it says.</param>
/// <param name="Detail">Its marks: ticked n of m, the stage and gates, Waiting on you.</param>
/// <param name="Children">What is under it.</param>
/// <param name="Row">The pipeline row selecting it scrolls to, or null.</param>
/// <param name="IsWaitingOnYou">Whether it, or something under it, waits on the owner.</param>
public sealed record ProjectTreeNodeViewModel(
    string Text,
    string Detail,
    IReadOnlyList<ProjectTreeNodeViewModel> Children,
    ProjectPipelineRowViewModel? Row,
    bool IsWaitingOnYou)
{
    /// <summary>Whether it has marks to show under its text; a criterion has none.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>For a criterion, whether it is ticked; null for every other item.</summary>
    public bool? IsTicked { get; init; }

    /// <summary>Whether it is a criterion, so it shows a tick box.</summary>
    public bool IsCriterion => IsTicked is not null;

    /// <summary>
    /// The criterion's box as the file has it. A glyph on a button, not a check box, so a refused tick never shows a
    /// state the file does not hold (1.1c review, item 7).
    /// </summary>
    public string TickGlyph => IsTicked == true ? "☑" : "☐";

    /// <summary>For a criterion, ticks or unticks it in the project file.</summary>
    public ICommand? ToggleCommand { get; init; }

    /// <summary>For an objective, Edit…: its title.</summary>
    public ICommand? EditCommand { get; init; }

    /// <summary>For a phase, Add objective….</summary>
    public ICommand? AddObjectiveCommand { get; init; }

    /// <summary>For a phase or an objective, Open project.md.</summary>
    public ICommand? OpenFileCommand { get; init; }

    /// <summary>Whether its right-click menu has Open project.md.</summary>
    public bool HasOpenFile => OpenFileCommand is not null;

    /// <summary>Whether its right-click menu has Edit….</summary>
    public bool HasEdit => EditCommand is not null;

    /// <summary>Whether its right-click menu has Add objective….</summary>
    public bool HasAddObjective => AddObjectiveCommand is not null;

    /// <summary>Whether its right-click menu has anything on it.</summary>
    public bool HasMenu => HasEdit || HasAddObjective || HasOpenFile;
}
