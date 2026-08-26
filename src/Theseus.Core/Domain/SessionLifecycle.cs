namespace Theseus.Core.Domain;

/// <summary>
/// What a card shows right now. Never persisted: it is computed from the newest run plus live
/// observation of the registry, so it can never disagree with reality after a crash or a reboot.
/// </summary>
public enum SessionLifecycle
{
    /// <summary>No run today.</summary>
    NotStarted,

    /// <summary>A tab was opened and the session has not registered yet.</summary>
    Launching,

    /// <summary>Registered and working.</summary>
    Running,

    /// <summary>Registered and waiting for input.</summary>
    Idle,

    /// <summary>Registered and asking for something.</summary>
    NeedsAttention,

    /// <summary>The session finished.</summary>
    Ended,

    /// <summary>A registry entry is present but its process is dead or was replaced.</summary>
    Ghost,
}
