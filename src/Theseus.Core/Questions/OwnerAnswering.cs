using Theseus.Core.Projects;

namespace Theseus.Core.Questions;

/// <summary>
/// The owner answers a session's question on the board (2.4e, Nigel 2026-11-26: "there is nothing to answer in those
/// sessions"). The answer goes the way tells and duties go: left in the tells folder, it is typed into a tab session's
/// next turn, taken by a headless one when its turn ends, or given with the start that resumes an ended one. It is logged
/// for events(), and the wait says it is answered until the hook or a start delivers it.
/// </summary>
public sealed class OwnerAnswering
{
    private readonly OwnerQuestionStore _questions;
    private readonly SessionTells _tells;

    /// <summary>Creates it over the questions the board shows and the tells the hook delivers.</summary>
    public OwnerAnswering(OwnerQuestionStore questions, SessionTells tells)
    {
        _questions = questions;
        _tells = tells;
    }

    /// <summary>Gives the owner's answer to the session that asked.</summary>
    /// <param name="target">The card, its session and how the answer can reach it.</param>
    /// <param name="answer">The answer: one of the options, or his own words.</param>
    /// <param name="now">The moment.</param>
    /// <param name="wake">Types the hand-in marker into a tab session; whether it was sent.</param>
    /// <param name="resume">Starts an ended card, which takes what waits for it; why not, or null when it started.</param>
    public OwnerAnswerOutcome Answer(OwnerAnswerTarget target, string answer, DateTimeOffset now, Func<bool> wake, Func<string?> resume)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(wake);
        ArgumentNullException.ThrowIfNull(resume);

        var given = answer?.Trim();

        if (string.IsNullOrEmpty(given))
        {
            return new OwnerAnswerOutcome(AnswerRoute.None, "Give an answer: choose an option or write one.", true);
        }

        if (_questions.Read(target.SessionId) is not { IsOpen: true } question)
        {
            return new OwnerAnswerOutcome(AnswerRoute.None, $"{target.CardTitle} has no question open to answer.", true);
        }

        if (!_tells.Tell(target.CardKey, target.CardTitle, isRunning: true, target.SessionId, OwnerAnswerText.For(question, given), now))
        {
            return new OwnerAnswerOutcome(AnswerRoute.None, $"The answer could not be left for {target.CardTitle}; nothing was sent.", true);
        }

        _questions.RecordAnswer(target.SessionId, given, now);

        switch (target.Reach)
        {
            case AnswerReach.Tab when wake():
                return new OwnerAnswerOutcome(AnswerRoute.TypedIntoTab, $"Answered: {target.CardTitle} takes it now.", false);
            case AnswerReach.Tab:
                return new OwnerAnswerOutcome(AnswerRoute.NextTurn,
                    $"Answered: it waits for {target.CardTitle}'s next turn; its tab could not be woken, so press Enter there.", false);
            case AnswerReach.Headless:
                return new OwnerAnswerOutcome(AnswerRoute.NextTurn, $"Answered: {target.CardTitle} takes it when its turn ends.", false);
            case AnswerReach.Ended when resume() is { } notStarted:
                return new OwnerAnswerOutcome(AnswerRoute.Kept,
                    $"Answered, waiting to be delivered: {target.CardTitle} could not start ({notStarted}), so it is given with its next start.", false);
            case AnswerReach.Ended:
                return new OwnerAnswerOutcome(AnswerRoute.Resumed, $"Answered: {target.CardTitle} is resumed with it.", false);
            default:
                return new OwnerAnswerOutcome(AnswerRoute.Kept,
                    $"Answered, waiting to be delivered: {target.CardTitle} is given it when it next runs.", false);
        }
    }
}
