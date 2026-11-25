namespace Theseus.Core.Settings;

/// <summary>
/// Which cards the board shows: everything, only the project cards, or only the workstreams outside a project (Nigel,
/// 2026-11-23: "a button to show only projects, or show only workstreams or show all"). Remembered with the board.
/// </summary>
public enum BoardShows
{
    /// <summary>Project cards and workstreams alike, as the board always has.</summary>
    All,

    /// <summary>Only the project cards.</summary>
    Projects,

    /// <summary>Only the workstreams on the grid: those in no project.</summary>
    Workstreams,
}
