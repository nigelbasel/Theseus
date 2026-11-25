namespace Theseus.Core.Stopping;

/// <summary>How far a stop under way has got, in the order a stop happens (close-step AC 6).</summary>
public enum StopStage
{
    /// <summary>The request is written; the session has not yet ended a turn to collect it.</summary>
    Asked,

    /// <summary>The session was handed the request and is writing its handoff.</summary>
    HandedOver,

    /// <summary>The handoff is written; the session closes once it has been quiet for long enough.</summary>
    HandoffWritten,

    /// <summary>/exit was typed.</summary>
    ExitTyped,

    /// <summary>/exit could not be typed.</summary>
    ExitFailed,
}
