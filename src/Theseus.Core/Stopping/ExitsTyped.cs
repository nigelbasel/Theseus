namespace Theseus.Core.Stopping;

/// <summary>
/// The stops Theseus has already typed /exit for, held for the life of the board. Typing it twice would land in
/// a session that had moved on to something else, and there is no taking a keystroke back.
///
/// Moved out of the shell unchanged (resumed-stop, 2026-11-18), so that what it remembers can be tested.
/// </summary>
public sealed class ExitsTyped
{
    private readonly HashSet<string> _sessions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether /exit has already been typed for this stop.</summary>
    public bool AlreadyTyped(PendingStopRequest? request) =>
        request is not null && _sessions.Contains(request.SessionId);

    /// <summary>Records that /exit has been typed for this stop.</summary>
    public void Typed(PendingStopRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        _sessions.Add(request.SessionId);
    }
}
