namespace Theseus.Core.Questions;

/// <summary>How an answer on the board can reach the session that asked (2.4e).</summary>
public enum AnswerReach
{
    /// <summary>Open in a terminal tab: typed into it, as a duty is handed in.</summary>
    Tab,

    /// <summary>A headless run going now: it takes the answer when its turn ends.</summary>
    Headless,

    /// <summary>Ended, and free to start: resumed with the answer.</summary>
    Ended,

    /// <summary>Stopped or paused by the owner, blocked or archived: kept until it next runs.</summary>
    HeldBack,
}
