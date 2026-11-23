namespace Theseus.Core.Projects;

/// <summary>
/// A confirmation that lists items, shown as a checklist rather than a message box (Nigel, 2026-11-23): one row per item,
/// each ticked to begin with so one can be left out, and a foot that says what the press will do for the rows ticked.
/// </summary>
/// <param name="Title">The dialog's title.</param>
/// <param name="Intro">What the press does, above the rows.</param>
/// <param name="ConfirmLabel">The press's own button.</param>
/// <param name="Rows">The items.</param>
/// <param name="Foot">What the press will do for the keys ticked, such as "Stop 3 and archive 17".</param>
public sealed record Checklist(
    string Title,
    string Intro,
    string ConfirmLabel,
    IReadOnlyList<ChecklistRow> Rows,
    Func<IReadOnlyList<string>, string> Foot);
