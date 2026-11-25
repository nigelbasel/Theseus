namespace Theseus.Core.Running;

/// <summary>Why a run was not started or queued.</summary>
public enum RefusalReason
{
    /// <summary>The card's or its project's budget for the day is spent.</summary>
    BudgetReached,

    /// <summary>The card's circuit breaker tripped, and only the owner's Reset clears it.</summary>
    BreakerTripped,

    /// <summary>The card is paused.</summary>
    Paused,

    /// <summary>The card already has a run going or queued.</summary>
    AlreadyRunning,

    /// <summary>The card could not be started for another reason, given in the detail.</summary>
    CannotStart,
}
