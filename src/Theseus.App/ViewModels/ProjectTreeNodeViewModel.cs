namespace Theseus.App.ViewModels;

/// <summary>
/// One item of the project view's tree: a phase, an objective (criteria ticked n of m) or a workstream slug, with its
/// passed gates and anything waiting on the owner marked (AC 15). Selecting it scrolls the pipeline to its row.
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
    bool IsWaitingOnYou);
