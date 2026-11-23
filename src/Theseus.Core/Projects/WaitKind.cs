namespace Theseus.Core.Projects;

/// <summary>What the owner does for one "Waits for you" item, which picks its button (1.1f G36, G37a).</summary>
public enum WaitKind
{
    /// <summary>Accept a piece's spec (a bug's rule).</summary>
    AcceptSpec,

    /// <summary>Confirm a spec he accepted in a session's tab (1.1e item 28).</summary>
    ConfirmAcceptance,

    /// <summary>Answer a session in its terminal.</summary>
    Answer,

    /// <summary>Look at a card marked blocked.</summary>
    Unblock,

    /// <summary>Add a linked card to an objective in project.md.</summary>
    AddToObjective,

    /// <summary>Confirm a report back on the board.</summary>
    ConfirmReport,

    /// <summary>Approve or reject a proposal.</summary>
    Proposal,

    /// <summary>Start again a verifier card whose start, made by Theseus as it made the card, failed.</summary>
    StartVerifier,

    /// <summary>Allow or refuse a session's start of a card he paused (sessions-run-the-project item 13).</summary>
    StartRequest,

    /// <summary>Clear up verifier cards that should not be kept: copies of a build's check, or checks of another project's holdout.</summary>
    ClearVerifiers,

    /// <summary>
    /// Builds whose independent check Theseus cannot start, with why; the view offers the way on, such as Set the holdout
    /// folder… or Write scenarios… (review of P1, blocker 1).
    /// </summary>
    CheckCannotRun,

    /// <summary>What Theseus is doing itself, such as writing scenarios or checking builds: said, with nothing to press.</summary>
    Underway,

    /// <summary>Something outside Theseus: no button.</summary>
    Outside,
}
