namespace Theseus.Core.Kanban;

/// <summary>
/// The kanban's columns, one order for every pipeline (projects-kanban spec AC 8, Nigel, 2026-11-26): a bug's
/// Reproduce, Rule and Fix share Research, Spec and Build, and freeform's Work is Build.
/// </summary>
public enum KanbanColumn
{
    /// <summary>Research; a bug's Reproduce.</summary>
    Research,

    /// <summary>Spec; a bug's Rule.</summary>
    Spec,

    /// <summary>Plan. Bugs and freeform work have none.</summary>
    Plan,

    /// <summary>Implement; a bug's Fix; freeform's Work.</summary>
    Build,

    /// <summary>Verify.</summary>
    Verify,

    /// <summary>Review.</summary>
    Review,

    /// <summary>Compound, shown as Lessons.</summary>
    Lessons,

    /// <summary>Every stage finished, or reported back and confirmed.</summary>
    Done,
}
