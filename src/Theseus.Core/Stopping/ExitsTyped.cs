namespace Theseus.Core.Stopping;

/// <summary>
/// The stops Theseus has already typed /exit for, held for the life of the board. Typing it twice into one stop
/// would land in a session that had moved on to something else, and there is no taking a keystroke back.
///
/// A stop is its session and the moment it was asked (resumed-stop rule 1, Nigel 2026-11-18). It was the session
/// alone, which meant a session resumed under the same id was never closed by a later stop for the rest of the
/// board's run: seen live on 2026-11-18.
/// </summary>
public sealed class ExitsTyped
{
    private readonly HashSet<(string Session, DateTimeOffset Asked)> _stops = new(new StopComparer());
    private readonly HashSet<(string Session, DateTimeOffset Asked)> _finished = new(new StopComparer());
    private readonly Dictionary<(string Session, DateTimeOffset Asked), ExitAttempt> _attempts = new(new StopComparer());
    private readonly Dictionary<string, ExitAttempt> _closing = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether /exit has already been tried for this stop, landed or not. It is never tried twice.</summary>
    public bool AlreadyTyped(PendingStopRequest? request) =>
        request is not null && _stops.Contains((request.SessionId, request.RequestedUtc));

    /// <summary>Records that /exit has been tried for this stop.</summary>
    public void Typed(PendingStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _stops.Add((request.SessionId, request.RequestedUtc));
    }

    /// <summary>
    /// Whether /exit landed for this stop, so that it has finished. Tried is not finished: keystrokes that did not
    /// land leave the stop unfinished, and it is reported as such when it is removed (resumed-stop review S1).
    /// </summary>
    public bool IsFinished(PendingStopRequest? request) =>
        request is not null && _finished.Contains((request.SessionId, request.RequestedUtc));

    /// <summary>Records that /exit landed for this stop.</summary>
    public void Finished(PendingStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _finished.Add((request.SessionId, request.RequestedUtc));
    }

    /// <summary>
    /// Records an attempt and what it did. It counts as tried whatever happened, so nothing retries it on its own:
    /// every attempt raises the terminal, which would take focus from the owner without warning (close-step AC 9).
    /// </summary>
    public void Attempted(PendingStopRequest request, ExitAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attempt);

        Typed(request);
        _attempts[(request.SessionId, request.RequestedUtc)] = attempt;

        if (attempt.Result == ExitResult.Sent)
        {
            Finished(request);
            _closing[request.SessionId] = attempt;
        }
    }

    /// <summary>
    /// The sent /exit for the run of this session now running, if there was one. The request is retired the moment
    /// /exit is sent, so without this the card lost its stop and offered Stop again while the session was still
    /// closing (close-step verify F1). A session started after the attempt is a later run, which was never asked.
    /// </summary>
    public ExitAttempt? Closing(string sessionId, DateTimeOffset sessionStartedUtc) =>
        _closing.TryGetValue(sessionId, out var attempt) && attempt.AtUtc >= sessionStartedUtc ? attempt : null;

    /// <summary>
    /// The owner pressed Close now. The attempt is made only if it is offered, and if the session turns out not to be
    /// ready at that moment the stop is marked tried again, so nothing retries it later unasked (close-step AC 9).
    /// </summary>
    /// <param name="request">The stop to try again.</param>
    /// <param name="attempt">Decides, and closes when it can; returns what it decided.</param>
    /// <returns>What was decided, or null when Close now was not offered and nothing was attempted.</returns>
    public CloseDecision? TryCloseNow(PendingStopRequest request, Func<CloseDecision> attempt)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(attempt);

        if (!AllowCloseNow(request))
        {
            return null;
        }

        var decision = attempt();

        if (decision.Verdict != CloseVerdict.Close)
        {
            Typed(request);
        }

        return decision;
    }

    /// <summary>The last attempt for this stop, if there was one.</summary>
    public ExitAttempt? LastAttempt(PendingStopRequest? request) =>
        request is not null && _attempts.TryGetValue((request.SessionId, request.RequestedUtc), out var attempt)
            ? attempt
            : null;

    /// <summary>
    /// Whether Close now is offered: the last attempt typed nothing for certain. Never after a sent attempt, and never
    /// after one Windows refused, which may have typed part of /exit (close-step AC 9 and 11).
    /// </summary>
    public bool MayCloseNow(PendingStopRequest? request) =>
        LastAttempt(request)?.Result is ExitResult.TabNotConfirmed or ExitResult.FocusLost;

    /// <summary>The owner pressed Close now: lets the close step try this stop exactly once more, if it is offered.</summary>
    public bool AllowCloseNow(PendingStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return MayCloseNow(request) && _stops.Remove((request.SessionId, request.RequestedUtc));
    }

    private sealed class StopComparer : IEqualityComparer<(string Session, DateTimeOffset Asked)>
    {
        public bool Equals((string Session, DateTimeOffset Asked) x, (string Session, DateTimeOffset Asked) y) =>
            string.Equals(x.Session, y.Session, StringComparison.OrdinalIgnoreCase) && x.Asked.UtcTicks == y.Asked.UtcTicks;

        public int GetHashCode((string Session, DateTimeOffset Asked) stop) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(stop.Session), stop.Asked.UtcTicks);
    }
}
