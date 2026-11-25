using System.IO;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Persistence;
using Theseus.Core.Projects;
using Theseus.Core.Stopping;
using Theseus.Core.Terminal;

namespace Theseus.App.ViewModels;

/// <summary>
/// Sessions that end without the owner closing them: a stop or a hand-in reaches an idle session by typing a marker into
/// its own console, so the hook gives it what waits; and a card whose work is finished is asked to hand over and exit,
/// as <see cref="CardEnding"/> decides.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>What is typed into an idle session to start the turn that carries work handed to it.</summary>
    public const string HandInMarker = "theseus-next";

    // Each session is ended by itself once: a session resumed later under the same id is a new run, judged again.
    private readonly HashSet<(string Session, DateTimeOffset Started)> _endedByThemselves = [];

    /// <summary>
    /// Types a marker into an idle session's console to start its turn. Nothing is raised and focus does not move, so this
    /// is what Theseus does when nobody pressed anything.
    /// </summary>
    private ConsoleTypingOutcome Wake(StopStep step, string marker)
    {
        var outcome = ConsoleTyping.TypeLine(step.Pid, marker);

        TheseusLog.Information("Woke {Workstream} by typing {Marker} into its console: {Outcome}", step.WorkstreamKey, marker, outcome);

        return outcome;
    }

    /// <summary>
    /// Whether typing into the tab may be tried after the console was not: only when nothing reached the console, since a
    /// line partly written there would be typed a second time.
    /// </summary>
    private static bool NothingTyped(ConsoleTypingOutcome outcome) =>
        outcome is ConsoleTypingOutcome.NotAttached or ConsoleTypingOutcome.NoInput;

    /// <summary>
    /// A session's start or resume of a card whose session is open and idle: the instruction waits in the tells folder,
    /// and the session is woken to take it as its next turn.
    /// </summary>
    private string? HandInForSession(WorkstreamCardViewModel card, string instruction, string caller)
    {
        if (!HookInstallation.IsInstalled(SettingsFile()))
        {
            return $"Theseus's stop hook is not installed, so {card.Title} cannot be handed work from here; the user must give it to it.";
        }

        if (StepsFor([card]).FirstOrDefault() is not { } step)
        {
            return $"{card.Title} has no session to hand it to.";
        }

        var from = Board.Card(caller)?.Title ?? caller;

        if (!Tells.Queue(card.Key, card.Title, step.SessionId, $"{from} hands you more work: {instruction}", DateTimeOffset.UtcNow))
        {
            return $"{card.Title}'s instruction could not be left for it; nothing was handed in.";
        }

        if (Wake(step, HandInMarker) != ConsoleTypingOutcome.Sent)
        {
            return $"{card.Title}'s instruction waits for its next turn, but its session could not be woken to take it: the user must press Enter in its tab.";
        }

        return null;
    }

    /// <summary>
    /// Asks each finished card to hand over and exit, and says why on its card. A card that would be finished once its
    /// quiet ends gives the time to look again.
    /// </summary>
    private void EndFinishedCards(DateTimeOffset now, List<DateTimeOffset> waiting)
    {
        if (!RunsChecksItself || !HookInstallation.IsInstalled(SettingsFile()))
        {
            return;
        }

        var links = Chains?.AllLinks ?? [];
        var leads = ProjectLeadKeys();

        foreach (var card in Board.Cards)
        {
            if (card.Lifecycle != SessionLifecycle.Idle
                || card.Workstream.LastSessionId is not { } sessionId
                || Sessions.Find(sessionId) is not { } session
                || !string.Equals(session.Status, "idle", StringComparison.OrdinalIgnoreCase)
                || _unconfirmed.Contains(sessionId)
                || _endedByThemselves.Contains((sessionId, session.StartedUtc)))
            {
                continue;
            }

            var facts = new CardEndingFacts(card.Key, card.Workstream.Role, card.Lifecycle, session.StatusSinceUtc, session.StartedUtc, links)
            {
                LastHandInUtc = _cardEvents.For(card.Key).Where(each => each.Instruction is not null).Select(each => (DateTimeOffset?)each.AtUtc).Max(),
                SomethingWaits = _stopRequests.Waiting(sessionId) is not null || _stopRequests.Delivered(sessionId) is not null
                                 || Tells.Waiting.Waiting(sessionId) is not null,
                QuestionOpen = Questions.Read(sessionId) is not null,
                IsLead = leads.Contains(card.Key),
                ScenariosWrittenUtc = card.Workstream.Role == WorkstreamRole.ScenarioWriter ? ScenariosWritten(card.Workstream.WorkingDirectory) : null,
            };

            if (CardEnding.Decide(facts, now) is not { } reason)
            {
                if (session.StatusSinceUtc is { } since && CardEnding.Decide(facts, since + CardEnding.Quiet) is not null)
                {
                    waiting.Add(since + CardEnding.Quiet);
                }

                continue;
            }

            if (StepsFor([card]).FirstOrDefault() is not { } step)
            {
                continue;
            }

            _endedByThemselves.Add((sessionId, session.StartedUtc));
            Ask(step);
            Wake(step, StopRouting.NudgeMarker);

            var said = CardEnding.Text(reason);
            _cardEvents.Append(new CardEvent(now, card.Key, said));
            TheseusLog.Information("{Workstream}: {Ending}", card.Key, said);
        }
    }

    private HashSet<string> ProjectLeadKeys()
    {
        var chosen = _settings.Read().ProjectLeads;

        return
        [
            .. _projectReads.Current.Select(reading => reading.File).OfType<ProjectFile>()
                .Select(file => ProjectLead.KeyOf(file, chosen)).OfType<string>(),
        ];
    }

    // The newest scenario a writer has written: the files of its holdout folder, leaving out verifiers' results and git's own.
    private static DateTimeOffset? ScenariosWritten(string folder)
    {
        try
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file))
                .Where(relative => relative.Split(Path.DirectorySeparatorChar)[0] is var top
                                   && !string.Equals(top, VerifierCard.ResultsFolder, StringComparison.OrdinalIgnoreCase)
                                   && !string.Equals(top, ".git", StringComparison.OrdinalIgnoreCase))
                .Select(relative => Path.Combine(folder, relative))
                .Select(file => (DateTimeOffset?)new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero))
                .Max();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Could not read the scenarios in {Folder}: {Reason}", folder, exception.Message);

            return null;
        }
    }
}
