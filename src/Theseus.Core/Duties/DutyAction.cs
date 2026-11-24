namespace Theseus.Core.Duties;

/// <summary>What the scheduler says to do with a duty now.</summary>
public enum DutyAction
{
    /// <summary>Nothing: it is not due, or a run is in flight.</summary>
    None,

    /// <summary>Give the card its run.</summary>
    Run,

    /// <summary>It is due but waits, for a busy card, a question or quiet hours.</summary>
    Wait,

    /// <summary>It is due and is skipped, with the reason recorded.</summary>
    Skip,

    /// <summary>It has reached its end.</summary>
    End,
}
