namespace Theseus.App.ViewModels;

/// <summary>A workstream that has just been created, and where on the board it belongs.</summary>
/// <param name="Key">The new workstream's key, which is its plan file's name.</param>
/// <param name="Stage">The column it was created in.</param>
/// <param name="Area">The row it was created in.</param>
/// <param name="StartNow">Whether a tab should be opened for it straight away.</param>
public sealed record NewWorkstream(string Key, string Stage, string Area, bool StartNow);
