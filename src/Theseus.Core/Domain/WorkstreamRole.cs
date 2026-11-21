namespace Theseus.Core.Domain;

/// <summary>
/// What a card's sessions are for, beyond its own work. A role reaches the session's hooks as an environment variable
/// the session cannot change, which is how the holdout guard tells a verifier from the work it checks (verifier-card
/// D1, Nigel 2026-11-19). Set by the board: no proposal or Theseus tool offers it, and the holdout guard refuses a
/// session's file-tool writes to the board file. A shell command could still edit that file: a tripwire, not a wall.
/// </summary>
public enum WorkstreamRole
{
    /// <summary>An ordinary card.</summary>
    None,

    /// <summary>Runs holdout scenarios against another card's change, and reports only a verdict back.</summary>
    Verifier,

    /// <summary>
    /// Writes a project's holdout scenarios, the one kind of session allowed to (1.1e AC 6c): its sessions run with
    /// THESEUS_HOLDOUT_AUTHOR=1, which the holdout guard lets through. Never a building session.
    /// </summary>
    ScenarioWriter,

    /// <summary>
    /// Brings a project's project.md into the method's current shape, and only that, then stops (1.1e AC 19): started by the
    /// owner's Update on a project card whose file does not read.
    /// </summary>
    ProjectUpdater,
}
