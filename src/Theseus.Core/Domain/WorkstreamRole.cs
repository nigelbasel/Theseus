namespace Theseus.Core.Domain;

/// <summary>
/// What a card's sessions are for, beyond its own work. A role reaches the session's hooks as an environment variable
/// the session cannot change, which is how the holdout guard tells a verifier from the work it checks (verifier-card
/// D1, Nigel 2026-11-19). Set only by the board, never by a session.
/// </summary>
public enum WorkstreamRole
{
    /// <summary>An ordinary card.</summary>
    None,

    /// <summary>Runs holdout scenarios against another card's change, and reports only a verdict back.</summary>
    Verifier,
}
