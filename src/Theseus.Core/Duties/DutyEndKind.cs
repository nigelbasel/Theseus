namespace Theseus.Core.Duties;

/// <summary>When a duty ends of itself. A session may always end one with end_duty, whatever this says.</summary>
public enum DutyEndKind
{
    /// <summary>It runs until it is ended or deleted.</summary>
    Never,

    /// <summary>It ends at the start of a local date.</summary>
    OnDate,

    /// <summary>It ends once a criterion of its card's objective is ticked.</summary>
    OnCriterion,
}
