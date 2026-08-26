namespace Theseus.Core.Sessions;

/// <summary>
/// The command line that asks Theseus to bring one session's terminal tab to the front and do
/// nothing else. Focusing a tab is done entirely through the window manager, so the request needs no
/// conversation with a running Theseus: a second copy can serve it and exit.
/// </summary>
public static class FocusRequest
{
    /// <summary>The switch that names the session to focus.</summary>
    public const string Switch = "--focus";

    /// <summary>The session a command line asks for, or null when it asks for the window instead.</summary>
    public static string? SessionIdIn(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], Switch, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                return arguments[index + 1].Trim();
            }
        }

        return null;
    }

    /// <summary>The arguments that ask for one session.</summary>
    public static string ArgumentsFor(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        return $"{Switch} \"{sessionId}\"";
    }
}
