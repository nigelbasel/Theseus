namespace Theseus.Core.Duties;

/// <summary>How a run went, as Theseus saw it. The session never reports this.</summary>
public enum DutyOutcome
{
    /// <summary>The card took the instruction and its turn ended.</summary>
    Finished,

    /// <summary>The session died during the run.</summary>
    Crashed,

    /// <summary>The instruction did not reach the card.</summary>
    NotDelivered,
}
