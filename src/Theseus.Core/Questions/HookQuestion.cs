using Theseus.Core.Stopping;

namespace Theseus.Core.Questions;

/// <summary>
/// What the hook does about questions on each event (1.6 spec items 4, 7 to 10): the owner typing in a tab answers
/// whatever it had open, an answer through the question tool answers an asked question, and a turn that ends asking in
/// prose leaves a guess. It never speaks and never blocks a turn.
/// </summary>
public static class HookQuestion
{
    /// <summary>Acts on one hook event.</summary>
    /// <param name="input">What the hook was given.</param>
    /// <param name="store">Where questions are kept.</param>
    /// <param name="readTail">The end of a transcript as text (<see cref="TranscriptTail.Read"/>), or null.</param>
    /// <param name="now">The moment, for a guess.</param>
    /// <param name="delivered">
    /// Whether this hook delivered a stop or a tell this turn, extending it: such a turn has not ended, so it is not
    /// judged. A turn another hook extended is judged as usual, so a guess left by its first ending is cleared by its
    /// last.
    /// </param>
    public static void OnEvent(StopHookInput input, OwnerQuestionStore store, Func<string?, string?> readTail, DateTimeOffset now,
        bool delivered)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(readTail);

        if (input.SessionId is not { } sessionId)
        {
            return;
        }

        if (input.HookEventName == "UserPromptSubmit")
        {
            store.Clear(sessionId);

            return;
        }

        if (input.HookEventName != "Stop" || delivered || input.AgentId is not null)
        {
            return;
        }

        var open = store.Read(sessionId);
        var tail = open is { IsGuess: false } || input.LastAssistantMessage is null ? readTail(input.TranscriptPath) : null;

        if (open is { IsGuess: false } asked && TranscriptTail.QuestionToolAnsweredSince(tail, asked.AskedUtc))
        {
            store.Clear(sessionId);
        }

        if (ProseQuestion.AskingLine(input.LastAssistantMessage ?? TranscriptTail.LastAssistantTextIn(tail)) is { } line)
        {
            store.Guess(new OwnerQuestion(sessionId, line, IsGuess: true, now));
        }
        else
        {
            store.ClearGuess(sessionId);
        }
    }
}
