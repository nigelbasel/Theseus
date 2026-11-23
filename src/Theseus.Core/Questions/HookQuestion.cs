using Theseus.Core.Stopping;

namespace Theseus.Core.Questions;

/// <summary>
/// What the hook does about questions on each event (1.6 spec items 4, 7 to 10): the owner typing in a tab answers
/// whatever it had open, and a turn that ends asking in prose leaves a guess. It never speaks and never blocks a turn.
/// </summary>
public static class HookQuestion
{
    /// <summary>Acts on one hook event.</summary>
    /// <param name="input">What the hook was given.</param>
    /// <param name="store">Where questions are kept.</param>
    /// <param name="lastText">The last assistant text of a transcript, or null.</param>
    /// <param name="now">The moment, for a guess.</param>
    /// <param name="delivered">Whether the hook delivered a stop or a tell this turn: a turn it extended is not judged.</param>
    public static void OnEvent(StopHookInput input, OwnerQuestionStore store, Func<string?, string?> lastText, DateTimeOffset now,
        bool delivered)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(lastText);

        if (input.SessionId is not { } sessionId)
        {
            return;
        }

        if (input.HookEventName == "UserPromptSubmit")
        {
            store.Clear(sessionId);

            return;
        }

        if (input.HookEventName != "Stop" || delivered || input.StopHookActive || input.AgentId is not null)
        {
            return;
        }

        if (ProseQuestion.AskingLine(lastText(input.TranscriptPath)) is { } line)
        {
            store.Guess(new OwnerQuestion(sessionId, line, IsGuess: true, now));
        }
        else
        {
            store.ClearGuess(sessionId);
        }
    }
}
