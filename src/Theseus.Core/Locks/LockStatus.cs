namespace Theseus.Core.Locks;

/// <summary>Where a ticket stands.</summary>
public enum LockStatus
{
    /// <summary>It holds the lock.</summary>
    Granted,

    /// <summary>It is in the queue.</summary>
    Waiting,

    /// <summary>It is over: released, cancelled, expired or dropped. The answer's detail says which.</summary>
    Ended,

    /// <summary>This Theseus has no record of the token.</summary>
    Unknown,

    /// <summary>The request itself was not acceptable. The answer's detail says why.</summary>
    Refused,
}
