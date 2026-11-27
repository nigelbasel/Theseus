using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Questions;
using Theseus.Core.Stopping;
using Theseus.Core.Terminal;

namespace Theseus.App.ViewModels;

/// <summary>
/// A question a session asked with ask_owner is answered in Theseus (2.4e, Nigel 2026-11-26: "The board is showing two
/// things waiting for me, to answer it buttons, but there is nothing to answer in those sessions"). Answer it opens the
/// question here; the answer goes as tells and duties go, and Go to its tab stays for a session that has one.
/// </summary>
public sealed partial class ShellViewModel
{
    private OwnerAnswering? _answering;

    /// <summary>Shows a question to answer, and gives back the model once it closes; set by the app.</summary>
    public Func<AnswerQuestionViewModel, AnswerQuestionViewModel?>? AskAnswer { get; set; }

    private OwnerAnswering Answering => _answering ??= new OwnerAnswering(Questions, Tells);

    /// <summary>
    /// Answer it on a card's wait: its open question opens here; a card stopped at a prompt, at the question tool in its
    /// tab, or with nothing recorded, is brought forward as before, since only its tab can answer that, and a marker typed
    /// into an open dialog would answer it.
    /// </summary>
    public void AnswerQuestion(string cardKey)
    {
        if (Board.Card(cardKey) is not { } card)
        {
            return;
        }

        if (card.Prompt is not null || card.Question is not { IsOpen: true } question || card.Workstream.LastSessionId is not { } sessionId
            || AskAnswer is not { } ask
            || string.Equals(Sessions.Find(sessionId)?.Status, "waiting", StringComparison.OrdinalIgnoreCase))
        {
            FocusCardCommand.Execute(card);

            return;
        }

        var reach = ReachOf(card);
        var model = ask(new AnswerQuestionViewModel(card.Title, question, reach == AnswerReach.Tab, ReachesWhen(reach)));

        if (model is null)
        {
            return;
        }

        if (model.GoToTab)
        {
            FocusCardCommand.Execute(card);

            return;
        }

        if (model.Answer is not { } answer)
        {
            return;
        }

        if (reach is AnswerReach.Tab or AnswerReach.Headless && !HookInstallation.IsInstalled(SettingsFile()))
        {
            Notice = $"Theseus's stop hook is not installed, so the answer cannot reach {card.Title}; answer it in its tab.";

            return;
        }

        var outcome = Answering.Answer(new OwnerAnswerTarget(card.Key, card.Title, sessionId, reach), answer, DateTimeOffset.UtcNow,
            wake: () => StepsFor([card]).FirstOrDefault() is { } step && Wake(step, HandInMarker) == ConsoleTypingOutcome.Sent,
            resume: () =>
            {
                StartMany([card.Workstream], trigger: "AnswerOnTheBoard");

                return Tells.Waiting.Waiting(sessionId) is null ? null : _launcher.LastRefusal ?? "it did not start";
            });

        TheseusLog.Information("Answered {Workstream}'s question on the board: {Route}", card.Key, outcome.Route);
        _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, card.Key, outcome.Said));
        Notice = outcome.Said;
        SyncLifecycles();
    }

    // A headless run going or queued first: it has no tab, though the registry may list its session.
    private AnswerReach ReachOf(WorkstreamCardViewModel card)
    {
        if (Headless?.Holds(card.Key) == true)
        {
            return AnswerReach.Headless;
        }

        if (card.IsLive)
        {
            return AnswerReach.Tab;
        }

        var settings = _settings.Read();
        var workstream = card.Workstream;
        var heldBack = workstream.IsPaused || workstream.IsBlocked || workstream.IsArchived
                       || settings.CardsStoppedByOwner.Contains(card.Key, StringComparer.OrdinalIgnoreCase)
                       || (workstream.Project is { } project && settings.ProjectRunStates.ContainsKey(project));

        return heldBack ? AnswerReach.HeldBack : AnswerReach.Ended;
    }

    private static string ReachesWhen(AnswerReach reach) => reach switch
    {
        AnswerReach.Tab => "Your answer is typed into its tab as its next turn.",
        AnswerReach.Headless => "It runs headless: your answer is given to it when its turn ends.",
        AnswerReach.Ended => "Its session has ended: it is resumed with your answer.",
        _ => "It is stopped: your answer is kept and given to it when it next runs.",
    };
}
