namespace Theseus.Core.Stopping;

/// <summary>What the close step does with one session on one pass (close-step, 2026-11-19).</summary>
public enum CloseVerdict
{
    /// <summary>Nothing to do now. The session is checked again when it changes.</summary>
    Skip,

    /// <summary>
    /// Everything holds except quiet. Nothing else will check again (an idle session writes nothing to the registry),
    /// so the close step schedules itself for the moment quiet ends.
    /// </summary>
    Wait,

    /// <summary>Type /exit.</summary>
    Close,

    /// <summary>The stop was handed to an earlier run of the session, so it is not this run's to finish.</summary>
    Retire,
}
