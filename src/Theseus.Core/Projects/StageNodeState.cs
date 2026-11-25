namespace Theseus.Core.Projects;

/// <summary>
/// Where one stage of a piece of work stands in the project view, worked out from its records, its evidence block and
/// its card's session (1.1d AC 4 and 5). Never stored.
/// </summary>
public enum StageNodeState
{
    /// <summary>Its record is written, or its gate has passed.</summary>
    Done,

    /// <summary>Waiting on the owner: a spec or rule to accept, or a session asking in its terminal.</summary>
    WaitingOnYou,

    /// <summary>A session is working on it now.</summary>
    Busy,

    /// <summary>
    /// Its last evidence block is anything but <c>pass</c> or <c>open</c>, including one that cannot be read; or it has no
    /// evidence though later records are written; or its card is marked blocked. Never by time alone.
    /// </summary>
    Stuck,

    /// <summary>evidence.md is written, and has no result block or its last block's result is <c>open</c>.</summary>
    HoldoutsNotRun,

    /// <summary>Nothing of it yet.</summary>
    ToDo,

    /// <summary>Done by the pieces this one handed work to, on a spec piece (1.1f E26).</summary>
    HandedOn,

    /// <summary>
    /// Skipped, with its reason: the project's approved spec stood in for it (1.1f E27, D10), or its session said so
    /// (E30a). Shown as "Not needed".
    /// </summary>
    NotNeeded,

    /// <summary>Before the piece's furthest recorded stage, with neither a record nor a skip (1.1f E30a).</summary>
    DoneNoRecord,

    /// <summary>The piece's records are kept outside the project (1.1f E28).</summary>
    RecordsElsewhere,

    /// <summary>Reported back and confirmed, its objective's criteria not yet ticked (1.1f E29, D11).</summary>
    Answered,

    /// <summary>A card linked into the project that no project file lists (1.1f F31).</summary>
    NotInProject,
}
