namespace Theseus.Core.Stopping;

/// <summary>
/// What one session is asked when it is told to stop.
///
/// The request reaches the session through its own prompt, typed or pasted by the user, and not
/// through another session. A session takes its instructions from its user, and rightly refuses to
/// let a peer override them: a peer that could say "stop and exit" could say anything, so a message
/// arriving from one carries no authority however it is worded. Theseus therefore prepares the
/// request and puts it in front of the user, who issues it.
/// </summary>
public static class StopRequest
{
    /// <summary>The placeholder replaced with the session's own handoff path.</summary>
    public const string HandoffPlaceholder = "{handoff}";

    /// <summary>What a stop request says when nothing else is configured.</summary>
    public const string DefaultStopPrompt =
        "Please stop what you are doing now. Write a handoff to " + HandoffPlaceholder
        + " covering what you did, what is unfinished, and what someone resuming should do next. "
        + "Then exit.";

    /// <summary>The message to give one session, with its handoff path filled in.</summary>
    public static string For(string? stopPrompt, StopTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);

        var body = string.IsNullOrWhiteSpace(stopPrompt) ? DefaultStopPrompt : stopPrompt.Trim();

        return body.Replace(HandoffPlaceholder, target.HandoffPath, StringComparison.Ordinal);
    }
}
