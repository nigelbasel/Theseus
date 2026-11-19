namespace Theseus.Core.Stopping;

/// <summary>
/// What an attempt to type /exit did. Only the first two typed nothing for certain, which is why only they can be
/// tried again, and only when the owner presses Close now (close-step AC 9).
/// </summary>
public enum ExitResult
{
    /// <summary>The keystrokes were sent to the session's own tab.</summary>
    Sent,

    /// <summary>The session's tab could not be identified, so nothing was typed.</summary>
    TabNotConfirmed,

    /// <summary>The terminal was not in front at the moment of typing, so nothing was typed.</summary>
    FocusLost,

    /// <summary>Windows would not accept the input, and may have taken part of it.</summary>
    Refused,

    /// <summary>The terminal runs elevated and Theseus does not, so Windows refused the keystrokes.</summary>
    Elevated,
}
