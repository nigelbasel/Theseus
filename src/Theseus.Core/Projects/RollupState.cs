namespace Theseus.Core.Projects;

/// <summary>Where an objective or a phase stands, rolled up (Nigel, 2026-11-22).</summary>
public enum RollupState
{
    /// <summary>Every criterion ticked; for a phase, every objective met.</summary>
    Met,

    /// <summary>Some criteria ticked; for a phase, some objectives met.</summary>
    PartlyMet,

    /// <summary>A session is working on it, or work is recorded.</summary>
    InProgress,

    /// <summary>A criterion waits on the owner, or a serving piece does; wins over in progress.</summary>
    WaitingForYou,

    /// <summary>No work recorded yet.</summary>
    NotStarted,
}
