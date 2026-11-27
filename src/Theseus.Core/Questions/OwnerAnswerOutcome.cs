namespace Theseus.Core.Questions;

/// <summary>What became of an answer on the board (2.4e).</summary>
/// <param name="Route">Which way it went.</param>
/// <param name="Said">What the board says of it.</param>
/// <param name="IsError">Whether it was not given at all.</param>
public sealed record OwnerAnswerOutcome(AnswerRoute Route, string Said, bool IsError);
