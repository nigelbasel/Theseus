namespace Theseus.Core.Stopping;

/// <summary>
/// What a hook writes and what it exits with, to hand a session the request waiting for it.
///
/// The shape depends entirely on the event, and getting it wrong is silent. A <c>Stop</c> hook
/// speaks by refusing: exit 2 keeps the session from ending its turn and feeds standard error back
/// to it, whereas the JSON reply a <c>UserPromptSubmit</c> hook uses parses cleanly on <c>Stop</c>
/// and is then ignored - the request is consumed and nothing happens, which is precisely how this
/// went wrong the first time it was tried in earnest.
/// </summary>
/// <param name="StandardOutput">What goes to standard output, usually nothing.</param>
/// <param name="StandardError">What goes to standard error, which only exit 2 carries anywhere.</param>
/// <param name="ExitCode">Zero to let the turn end, two to block it.</param>
public sealed record StopHookDelivery(string StandardOutput, string StandardError, int ExitCode)
{
    /// <summary>Say nothing, and let the session get on with whatever it was doing.</summary>
    public static StopHookDelivery Silent { get; } = new(string.Empty, string.Empty, 0);

    /// <summary>How to deliver <paramref name="text"/> on the event that is running.</summary>
    public static StopHookDelivery For(string? hookEventName, string text) => hookEventName switch
    {
        "Stop" or "SubagentStop" => new(string.Empty, text, 2),
        _ => new(StopHookReply.For(hookEventName ?? "UserPromptSubmit", text), string.Empty, 0),
    };
}
