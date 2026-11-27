namespace Theseus.Core.Questions;

/// <summary>What a session is told when the owner answers its question on the board (2.4e).</summary>
public static class OwnerAnswerText
{
    /// <summary>The turn that carries the answer: the question it answers, then the answer.</summary>
    public static string For(OwnerQuestion question, string answer)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentException.ThrowIfNullOrWhiteSpace(answer);

        return $"The owner answered your question on the Theseus board.{Environment.NewLine}"
               + $"You asked: {question.Text}{Environment.NewLine}"
               + $"His answer: {answer}{Environment.NewLine}"
               + "It no longer shows on the board, so there is no need to call ask_owner with answered: true. Carry on with it.";
    }
}
