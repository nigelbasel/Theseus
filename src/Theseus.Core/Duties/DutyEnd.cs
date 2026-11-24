namespace Theseus.Core.Duties;

/// <summary>When a duty ends of itself.</summary>
/// <param name="Kind">Never, on a date, or on a criterion.</param>
/// <param name="Date">For OnDate: the local date it ends at the start of.</param>
/// <param name="Criterion">For OnCriterion: the criterion's text, as the project file has it.</param>
public sealed record DutyEnd(DutyEndKind Kind, DateOnly? Date = null, string? Criterion = null)
{
    /// <summary>A duty that runs until it is ended.</summary>
    public static readonly DutyEnd Never = new(DutyEndKind.Never);

    /// <summary>The end in words.</summary>
    public string Describe() => Kind switch
    {
        DutyEndKind.OnDate => $"until {Date:yyyy-MM-dd}",
        DutyEndKind.OnCriterion => $"until ticked: {Criterion}",
        _ => "no end",
    };
}
