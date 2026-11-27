namespace Theseus.Core.Projects;

/// <summary>How a project was put away (archive-a-project, Nigel 2026-11-26: "there is no way to archive or close a project").</summary>
public enum ProjectClosureKind
{
    /// <summary>Its cards stopped, its duties and queue paused, and it left the board's projects; Reopen puts it back.</summary>
    Closed,

    /// <summary>As closed, and its cards archived too; its records are kept where they are.</summary>
    Archived,
}
