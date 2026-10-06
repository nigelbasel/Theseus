namespace Theseus.Core.Locks;

/// <summary>A request for a lock, as a client sends it.</summary>
/// <param name="Lock">Which lock, one of <see cref="LockNames.All"/>.</param>
/// <param name="Who">Who is asking: the session's name, so Nigel can see who holds it.</param>
/// <param name="Purpose">What it is for: the suite, build or task.</param>
/// <param name="Mode">Whether a process or a session will hold it.</param>
/// <param name="WorkingDirectory">Where the work runs, which tells two requests from one session apart.</param>
/// <param name="SessionId">The Claude session asking, required for <see cref="HoldMode.Session"/>.</param>
/// <param name="Reason">Why it matters, shown on the board; it never changes the order.</param>
public sealed record LockRequest(
    string Lock,
    string Who,
    string Purpose,
    HoldMode Mode,
    string? WorkingDirectory = null,
    string? SessionId = null,
    string? Reason = null);
