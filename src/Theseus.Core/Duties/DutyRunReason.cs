namespace Theseus.Core.Duties;

/// <summary>Why Theseus gives a card a run of its own accord. Named as objective 2.2's runner names them.</summary>
public enum DutyRunReason
{
    /// <summary>A duty came due, or the owner pressed Run now.</summary>
    Duty,

    /// <summary>The overnight queue resumed an idle card with unfinished work.</summary>
    Queue,
}
