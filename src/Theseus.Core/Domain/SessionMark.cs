namespace Theseus.Core.Domain;

/// <summary>
/// A session's state in one mark, beside the ⌖ Focus that reaches its tab in one click, on a Chains row and a project
/// view box (project-view-usability item 8, Nigel 2026-11-23: four clicks to reach a card's tab "really is not great").
/// </summary>
public static class SessionMark
{
    /// <summary>"● running", "○ idle" or "◆ waiting" (asking the owner something); empty with no session.</summary>
    public static string Of(SessionLifecycle lifecycle) => lifecycle switch
    {
        SessionLifecycle.Launching or SessionLifecycle.Running => "● running",
        SessionLifecycle.Idle => "○ idle",
        SessionLifecycle.NeedsAttention => "◆ waiting",
        _ => string.Empty,
    };

    /// <summary>Whether there is a session to focus.</summary>
    public static bool HasSession(SessionLifecycle lifecycle) => Of(lifecycle).Length > 0;
}
