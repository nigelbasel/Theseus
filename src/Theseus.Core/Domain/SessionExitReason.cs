namespace Theseus.Core.Domain;

/// <summary>Why a recorded run ended.</summary>
public enum SessionExitReason
{
    /// <summary>Not established.</summary>
    Unknown,

    /// <summary>Claude Code exited with a zero status.</summary>
    ClosedCleanly,

    /// <summary>Claude Code exited non-zero, or its process disappeared.</summary>
    CrashedOrKilled,

    /// <summary>The tab was launched but no session ever registered.</summary>
    NeverStarted,

    /// <summary>The run ended while the application was not watching, so the outcome is unrecoverable.</summary>
    TheseusWasDown,
}
