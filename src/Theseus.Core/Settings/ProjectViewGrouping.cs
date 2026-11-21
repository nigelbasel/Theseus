namespace Theseus.Core.Settings;

/// <summary>
/// How a project's view groups its pieces of work, kept per project like the board's View › Rows by (1.1d AC 45;
/// Nigel's decision 5, 2026-11-21).
/// </summary>
public enum ProjectViewGrouping
{
    /// <summary>The tree of hand-overs, time running down the page.</summary>
    Flow,

    /// <summary>Under the project file's phases and objectives, as headings that fold.</summary>
    Objectives,
}
