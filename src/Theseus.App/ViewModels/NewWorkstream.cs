namespace Theseus.App.ViewModels;

/// <summary>A workstream that has just been created, and where on the board it belongs.</summary>
/// <param name="Key">The new workstream's key, which is its plan file's name.</param>
/// <param name="Stage">The column it was created in.</param>
/// <param name="Area">The row it was created in.</param>
/// <param name="StartNow">Whether a tab should be opened for it straight away.</param>
/// <param name="ParentKey">The workstream it was spawned from, when it was made as a follow-on.</param>
/// <param name="ReportTo">Where it writes the report its parent waits on, when it reports back.</param>
public sealed record NewWorkstream(string Key, string Stage, string Area, bool StartNow,
    string? ParentKey = null, string? ReportTo = null);
