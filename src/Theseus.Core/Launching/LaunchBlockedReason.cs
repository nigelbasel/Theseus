namespace Theseus.Core.Launching;

/// <summary>Why a workstream may not be launched right now.</summary>
public enum LaunchBlockedReason
{
    /// <summary>Nothing is blocking the launch.</summary>
    None,

    /// <summary>
    /// The session is still running. Resuming it would fork the transcript silently, so the board
    /// offers Focus instead of Restart.
    /// </summary>
    AlreadyLive,

    /// <summary>The workstream has no working directory, or it no longer exists on disk.</summary>
    WorkingDirectoryMissing,
}
