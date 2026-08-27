namespace Theseus.Core.Stopping;

/// <summary>How a stop request will actually reach one session.</summary>
public enum StopRoute
{
    /// <summary>Nothing to do: no session, or one the board does not follow.</summary>
    None,

    /// <summary>
    /// The session is working. The request is left for the hook, which delivers it when the turn
    /// ends - so the handoff is written from a settled state rather than mid-edit.
    /// </summary>
    WaitForTurnEnd,

    /// <summary>
    /// The session is idle and will take no turn on its own, so nothing will fire the hook. One
    /// keystroke starts a turn; the hook supplies the request.
    /// </summary>
    Nudge,

    /// <summary>
    /// The session is waiting on the user. It is at a prompt, where a keystroke is an answer to a
    /// question rather than a message, so it is brought to the front and left alone. The request
    /// keeps: answering resumes the turn, and the hook delivers it at the end of that turn.
    /// </summary>
    AnswerItFirst,

    /// <summary>
    /// No hook is installed, so nothing will deliver the request whatever the session is doing. The
    /// request goes on the clipboard and the tab comes forward.
    /// </summary>
    PasteYourself,
}
