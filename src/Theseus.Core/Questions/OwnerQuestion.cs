namespace Theseus.Core.Questions;

/// <summary>
/// A question a session waits on the owner to answer (1.6, Nigel 2026-11-22: "Questions being asked should always show
/// up in theseus"). Asked with <c>ask_owner</c>, or guessed by the stop hook from a turn that ended asking in prose.
/// </summary>
/// <param name="SessionId">The session asking.</param>
/// <param name="Text">The question, one line.</param>
/// <param name="IsGuess">Whether the stop hook guessed it from prose rather than the session asking it outright.</param>
/// <param name="AskedUtc">When it was recorded.</param>
public sealed record OwnerQuestion(string SessionId, string Text, bool IsGuess, DateTimeOffset AskedUtc)
{
    /// <summary>The answers the session offers, at most four; empty for an open question.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>Why the session needs the answer, one line, or null.</summary>
    public string? Why { get; init; }

    /// <summary>
    /// The owner's answer given on the board, kept until it is delivered to the session (2.4e, Nigel 2026-11-26); null
    /// while it is still his to answer.
    /// </summary>
    public string? Answer { get; init; }

    /// <summary>When the owner answered it on the board, or null.</summary>
    public DateTimeOffset? AnsweredUtc { get; init; }

    /// <summary>Whether it still waits on the owner: asked or guessed, and not yet answered on the board.</summary>
    public bool IsOpen => AnsweredUtc is null;
}
