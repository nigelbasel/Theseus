namespace Theseus.Core.Locks;

/// <summary>A request for a lock, as a client sends it.</summary>
/// <param name="Lock">A predefined scope, one of <see cref="LockNames.All"/>, or empty when <paramref name="Scope"/> says it.</param>
/// <param name="Who">Who is asking: the session's name, so Nigel can see who holds it.</param>
/// <param name="Purpose">What it is for: the suite, build or task.</param>
/// <param name="Mode">Whether a process or a session will hold it.</param>
/// <param name="WorkingDirectory">Where the work runs, which tells two requests from one session apart.</param>
/// <param name="SessionId">The Claude session asking, required for <see cref="HoldMode.Session"/>.</param>
/// <param name="Reason">Why it matters, shown on the board; it never changes the order.</param>
/// <param name="Scope">Resources asked for, added to the predefined scope; the least scope of the command is added too.</param>
/// <param name="Owner">The Claude session the request comes from, when the script knows it.</param>
/// <param name="Card">The card asking.</param>
/// <param name="Command">The command line the hold will run.</param>
/// <param name="Commit">The repository's commit.</param>
/// <param name="Again">A second run of the same command on the same commit, asked for on purpose.</param>
public sealed record LockRequest(
    string Lock,
    string Who,
    string Purpose,
    HoldMode Mode,
    string? WorkingDirectory = null,
    string? SessionId = null,
    string? Reason = null,
    IReadOnlyList<ResourceClaim>? Scope = null,
    string? Owner = null,
    string? Card = null,
    string? Command = null,
    string? Commit = null,
    bool Again = false);
