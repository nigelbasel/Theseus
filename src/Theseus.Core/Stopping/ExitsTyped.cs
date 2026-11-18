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

    private sealed class StopComparer : IEqualityComparer<(string Session, DateTimeOffset Asked)>
    {
        public bool Equals((string Session, DateTimeOffset Asked) x, (string Session, DateTimeOffset Asked) y) =>
            string.Equals(x.Session, y.Session, StringComparison.OrdinalIgnoreCase) && x.Asked.UtcTicks == y.Asked.UtcTicks;

        public int GetHashCode((string Session, DateTimeOffset Asked) stop) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(stop.Session), stop.Asked.UtcTicks);
    }
}
