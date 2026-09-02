namespace Theseus.Core.Sessions;

/// <summary>
/// What to say when a card has been renamed but its session has not.
///
/// A tab's title is fixed when it is opened. Windows Terminal has no way to rename one from outside:
/// the command line has no verb for it, and the action that exists is reachable only from a
/// keybinding or the command palette. So renaming a workstream while it is running leaves the card
/// saying one thing and the tab saying another until the session is next started.
///
/// Nothing breaks — sessions are matched to tabs by the session's own name, not by the card's — but
/// a discrepancy nobody explains reads as a fault. This explains it.
/// </summary>
public static class RunningAs
{
    /// <summary>The note for a card, or nothing when the card and its session agree.</summary>
    public static string For(string? cardTitle, string? sessionName)
    {
        var card = ConsoleTitle.Clean(cardTitle);
        var session = ConsoleTitle.Clean(sessionName);

        if (card.Length == 0 || session.Length == 0
            || string.Equals(card, session, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return $"running as “{session}” until it is restarted";
    }
}
