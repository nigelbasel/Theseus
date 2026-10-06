namespace Theseus.Core.Locks;

/// <summary>Something that happened to a ticket, as the lock history records it.</summary>
public enum LockEventKind
{
    /// <summary>It joined a queue.</summary>
    Requested,

    /// <summary>It reached the front and was given the lock.</summary>
    Granted,

    /// <summary>The wrapper process took up a reserved grant.</summary>
    Attached,

    /// <summary>Its holder let it go.</summary>
    Released,

    /// <summary>The process or session holding it ended without releasing it.</summary>
    HolderGone,

    /// <summary>It was granted but nobody took it up in time.</summary>
    ReservationExpired,

    /// <summary>A waiter stopped asking and was taken out of the queue.</summary>
    WaiterDropped,

    /// <summary>Nigel moved it in the queue.</summary>
    Reordered,

    /// <summary>Nigel cancelled it while it waited.</summary>
    Cancelled,

    /// <summary>Nigel released it while it was held.</summary>
    ForceReleased,
}
