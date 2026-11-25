namespace Theseus.Core.Links;

/// <summary>
/// What a Chains row says about its card, in one mark and word, so a greyed Focus is no longer the only sign that a card
/// is inactive (chains-panel-at-scale criterion 12, Nigel 2026-11-25). Listed in the order they are tried.
/// </summary>
public enum ChainRowState
{
    /// <summary>Its session is asking the owner something.</summary>
    AskingYou,

    /// <summary>Its report waits for the owner's Confirm.</summary>
    Reported,

    /// <summary>Its report is lost: it stopped without reporting and has been finished with.</summary>
    ReportLost,

    /// <summary>A project session asked to start it and the start waits for the owner's Allow.</summary>
    StartAsked,

    /// <summary>Its tab is open and the session has not registered yet.</summary>
    Starting,

    /// <summary>Its session is working.</summary>
    Running,

    /// <summary>Its session is waiting for input.</summary>
    Idle,

    /// <summary>Put away.</summary>
    Archived,

    /// <summary>Paused, with no session.</summary>
    Paused,

    /// <summary>Blocked, with no session.</summary>
    Blocked,

    /// <summary>Done, with no session: its report back confirmed, or in its last stage.</summary>
    Done,

    /// <summary>Every input accepted, and not run since.</summary>
    Ready,

    /// <summary>An input not yet accepted.</summary>
    WaitingOnInputs,

    /// <summary>No session, and none of the above.</summary>
    Stopped,
}
