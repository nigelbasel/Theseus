namespace Theseus.Core.Questions;

/// <summary>
/// How a question reads wherever it shows, from one place so the card, Waits for you and the piece say the same thing
/// (1.1f's principle, one list). A guess says it is one, because the hook can be wrong.
/// </summary>
public static class QuestionWords
{
    /// <summary>The card's status line: "Asks: …", or "Probably asking you something: …".</summary>
    public static string Card(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question.IsGuess ? $"Probably asking you something: {question.Text}" : $"Asks: {question.Text}";
    }

    /// <summary>A line naming who asks: "W7 asks: …", or "W7 is probably asking you something: …".</summary>
    public static string Named(string who, OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question.IsGuess ? $"{who} is probably asking you something: {question.Text}" : $"{who} asks: {question.Text}";
    }

    /// <summary>Who acts next, on the piece in the project view.</summary>
    public static string WhoActs(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        return question.IsGuess
            ? "You, probably: answer it in its tab if it asks."
            : "You; it does nothing until you answer in its tab.";
    }

    /// <summary>The card's tooltip: the question, its options and why, each on a line.</summary>
    public static string Detail(OwnerQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var lines = new List<string> { Card(question) };

        if (question.Options.Count > 0)
        {
            lines.Add($"Options: {string.Join(" · ", question.Options)}");
        }

        if (!string.IsNullOrWhiteSpace(question.Why))
        {
            lines.Add($"Why: {question.Why}");
        }

        if (question.IsGuess)
        {
            lines.Add("Guessed from the end of its last turn: it did not ask with the question tool.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
