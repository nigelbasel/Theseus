namespace Theseus.Core.Projects;

/// <summary>Where a workstream's owner's gate stands, for the version on disk now (1.1e AC 28, 29).</summary>
public enum OwnerGateStatus
{
    /// <summary>No spec is written yet.</summary>
    NotWritten,

    /// <summary>Ready, waiting for the owner.</summary>
    WaitingForOwner,

    /// <summary>The session reported the owner accepted it in its tab; it waits for him to confirm on the board.</summary>
    ReportedWaitingForConfirmation,

    /// <summary>Accepted at this version.</summary>
    Accepted,

    /// <summary>An earlier version was accepted; this one is not.</summary>
    ChangedSinceAccepted,
}
