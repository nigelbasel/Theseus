namespace Theseus.Core.Questions;

/// <summary>Which way an answer on the board went (2.4e).</summary>
public enum AnswerRoute
{
    /// <summary>Not given: nothing to answer, or it could not be left for the session.</summary>
    None,

    /// <summary>Typed into its tab, which takes it now.</summary>
    TypedIntoTab,

    /// <summary>Waiting for the session's next turn.</summary>
    NextTurn,

    /// <summary>The session was resumed, and its start took the answer.</summary>
    Resumed,

    /// <summary>Kept until the session next runs.</summary>
    Kept,
}
