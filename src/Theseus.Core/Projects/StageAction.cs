namespace Theseus.Core.Projects;

/// <summary>What the owner can do from a stage of a piece of work (1.1d AC 2), each run by an existing command.</summary>
public enum StageAction
{
    /// <summary>Shows the spec (a bug's rule) that Accept spec accepts.</summary>
    OpenSpec,

    /// <summary>Accepts the spec as it now reads, after asking.</summary>
    AcceptSpec,

    /// <summary>Starts a verifier card for the card's current piece of work.</summary>
    VerifyWithHoldouts,

    /// <summary>Brings the card's terminal forward.</summary>
    Focus,

    /// <summary>Starts the card.</summary>
    Start,

    /// <summary>Asks the card's session to write its handover and stop.</summary>
    Stop,

    /// <summary>Opens the piece's records folder.</summary>
    OpenRecords,

    /// <summary>Opens the stage's own record.</summary>
    OpenRecord,
}
