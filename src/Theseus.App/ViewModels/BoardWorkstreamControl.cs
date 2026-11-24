using Theseus.Core.Domain;
using Theseus.Core.Mcp;
using Theseus.Core.Persistence;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a session does to the cards it may act on (its descendants, or in a project every card of it: sessions-run-the-
/// project item 8), carried out on the board (1.1e AC 14 to 16): a start that
/// resumes its conversation with the instruction for that start only, counted only once it has launched; a stop by the
/// stop request alone; a pause marked with the calling card and why. The state is checked again here, on the board's
/// thread, since the board may have changed since the session looked (review of PR 3, finding 3). Every call that is
/// done is said in the status bar after the board's own words, and kept in the card's History and in Theseus's log.
/// </summary>
public sealed class BoardWorkstreamControl : IWorkstreamControl
{
    private readonly BoardViewModel _board;
    private readonly Func<IReadOnlyList<Workstream>, string, string?> _start;
    private readonly Func<WorkstreamCardViewModel, string?> _stop;
    private readonly Func<WorkstreamCardViewModel, string, string, string?> _handIn;
    private readonly CardEventLog _events;
    private readonly Action<string> _notice;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action _refresh;

    /// <summary>Creates the control.</summary>
    /// <param name="board">The board.</param>
    /// <param name="start">Starts workstreams, resuming their conversations; why nothing launched, or null once it did.</param>
    /// <param name="stop">Sends a card's session the stop request; why it could not, or null.</param>
    /// <param name="handIn">Gives an open, idle session an instruction as its next turn, from the calling card; why it could not, or null.</param>
    /// <param name="events">Where each call is kept for the card's History.</param>
    /// <param name="notice">The status bar.</param>
    /// <param name="clock">The time now.</param>
    /// <param name="refresh">Draws the board again after a mark changes.</param>
    public BoardWorkstreamControl(BoardViewModel board, Func<IReadOnlyList<Workstream>, string, string?> start,
        Func<WorkstreamCardViewModel, string?> stop, Func<WorkstreamCardViewModel, string, string, string?> handIn,
        CardEventLog events, Action<string> notice, Func<DateTimeOffset> clock, Action refresh)
    {
        _board = board;
        _start = start;
        _stop = stop;
        _handIn = handIn;
        _events = events;
        _notice = notice;
        _clock = clock;
        _refresh = refresh;
    }

    /// <inheritdoc />
    public string? Start(string key, string? instruction, string caller)
    {
        if (Refusal(key, WorkstreamCall.Start, caller, instruction) is { } refusal)
        {
            return refusal;
        }

        var card = _board.Card(key)!;

        if (card.IsLive)
        {
            return HandIn(card, instruction!, caller);
        }

        var verb = string.IsNullOrWhiteSpace(card.Workstream.LastSessionId) ? "Started" : "Restarted";

        if (_start([card.Workstream with { StartInstruction = Instruction(instruction) }], "StartedBySession") is { } notStarted)
        {
            return notStarted;
        }

        Say(card, Said(verb, caller, instruction), caller, Instruction(instruction));

        return null;
    }

    /// <inheritdoc />
    public string? Stop(string key, string caller)
    {
        if (Refusal(key, WorkstreamCall.Stop, caller) is { } refusal)
        {
            return refusal;
        }

        var card = _board.Card(key)!;

        if (_stop(card) is { } notStopped)
        {
            return notStopped;
        }

        Say(card, $"Stopped by {TitleOf(caller)}");

        return null;
    }

    /// <inheritdoc />
    public string? Pause(string key, string caller, string? reason)
    {
        if (Refusal(key, WorkstreamCall.Pause, caller) is { } refusal)
        {
            return refusal;
        }

        var card = _board.Card(key)!;
        var live = card.IsLive;
        card.Workstream = card.Workstream with { IsPaused = true, PausedBy = caller, PauseReason = Instruction(reason) };
        _board.Persist(card);

        if (live && _stop(card) is { } notStopped)
        {
            Say(card, Said("Paused", caller, reason));
            _refresh();

            return $"{card.Title} is marked paused, but its session was not asked to stop: {notStopped}";
        }

        Say(card, Said("Paused", caller, reason));
        _refresh();

        return null;
    }

    /// <inheritdoc />
    public string? Resume(string key, string? instruction, string caller)
    {
        if (Refusal(key, WorkstreamCall.Resume, caller, instruction) is { } refusal)
        {
            return refusal;
        }

        var card = _board.Card(key)!;
        var paused = card.Workstream;
        card.Workstream = paused with { IsPaused = false, PausedBy = null, PauseReason = null };
        _board.Persist(card);

        if (card.IsLive)
        {
            if (HandIn(card, instruction!, caller) is { } notHanded)
            {
                card.Workstream = card.Workstream with { IsPaused = true, PausedBy = paused.PausedBy, PauseReason = paused.PauseReason };
                _board.Persist(card);
                _refresh();

                return notHanded;
            }

            _refresh();

            return null;
        }

        if (_start([card.Workstream with { StartInstruction = Instruction(instruction) }], "ResumedBySession") is { } notStarted)
        {
            // Nothing launched, so the pause stands (review of PR 3, finding 5).
            card.Workstream = card.Workstream with { IsPaused = true, PausedBy = paused.PausedBy, PauseReason = paused.PauseReason };
            _board.Persist(card);
            _refresh();

            return notStarted;
        }

        Say(card, Said("Resumed", caller, instruction), caller, Instruction(instruction));

        return null;
    }

    /// <summary>The card's state as the board has it now, against what the call needs; null when it may go ahead.</summary>
    private string? Refusal(string key, WorkstreamCall call, string caller, string? instruction = null)
    {
        if (_board.Card(key) is not { } card)
        {
            return $"{key} is not on the board just now (it may be archived, or the board is showing archived cards); ask again later.";
        }

        var workstream = card.Workstream;

        // In a project any session's pause may be taken over or cleared (sessions-run-the-project item 9).
        var callersProject = _board.Card(caller)?.Workstream.Project;
        var inProject = callersProject is not null
                        && string.Equals(workstream.Project, callersProject, StringComparison.OrdinalIgnoreCase);

        // A card that left the caller's project since the session looked is no longer its to act on (item 11).
        if (callersProject is not null && !inProject)
        {
            return $"{card.Title} is not a card of {callersProject}: another project's cards and ad hoc cards are the user's to start or stop.";
        }

        return call switch
        {
            _ when workstream.IsArchived => $"{card.Title} is archived.",
            WorkstreamCall.Start or WorkstreamCall.Resume
                when LiveStart.Refusal(card.Title, card.IsLive, card.Lifecycle, instruction) is { } open => open,
            WorkstreamCall.Start or WorkstreamCall.Resume when workstream.IsBlocked => $"{card.Title} is blocked: the user marked it so.",
            WorkstreamCall.Start or WorkstreamCall.Resume or WorkstreamCall.Pause when workstream is { IsPaused: true, PausedBy: null }
                => $"{card.Title} is paused by the user.",
            WorkstreamCall.Start when workstream.PausedBy is { } by => $"{card.Title} is paused by {TitleOf(by)}.",
            WorkstreamCall.Pause when !inProject && workstream.PausedBy is { } by && !Same(by, caller) => $"{card.Title} is paused by {TitleOf(by)}.",
            WorkstreamCall.Resume when inProject && workstream.PausedBy is null => $"{card.Title} is not paused.",
            WorkstreamCall.Resume when !inProject && !Same(workstream.PausedBy, caller) => $"{card.Title} is not paused by you.",
            WorkstreamCall.Stop when !card.IsLive => $"{card.Title} has no session to stop.",
            _ => null,
        };
    }

    // An open, idle session takes the instruction as its next turn, kept as a hand-in like a start's.
    private string? HandIn(WorkstreamCardViewModel card, string instruction, string caller)
    {
        if (_handIn(card, instruction.Trim(), caller) is { } notHanded)
        {
            return notHanded;
        }

        Say(card, Said("Handed in", caller, instruction), caller, Instruction(instruction));

        return null;
    }

    private static bool Same(string? one, string? other) => string.Equals(one, other, StringComparison.OrdinalIgnoreCase);

    private string TitleOf(string key) => _board.Card(key)?.Title ?? key;

    // "Restarted by Theseus: more work: the chips", with the first line of what was given (AC 16).
    private string Said(string verb, string caller, string? what) =>
        string.IsNullOrWhiteSpace(what)
            ? $"{verb} by {TitleOf(caller)}"
            : $"{verb} by {TitleOf(caller)}: {what.Trim().Split('\n')[0].Trim()}";

    private static string? Instruction(string? instruction) => string.IsNullOrWhiteSpace(instruction) ? null : instruction.Trim();

    // A start or resume given an instruction keeps all of it, and who gave it: the project view shows it as a hand-in.
    private void Say(WorkstreamCardViewModel card, string what, string? by = null, string? instruction = null)
    {
        _events.Append(new CardEvent(_clock(), card.Key, what) { By = instruction is null ? null : by, Instruction = instruction });
        Theseus.Core.Diagnostics.TheseusLog.Information("{Card}: {What}", card.Key, what);
        _notice($"{card.Title}: {what}.");
    }
}
