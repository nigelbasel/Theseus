namespace Theseus.App.Services;

/// <summary>How far an attempt to start an idle session's turn got.</summary>
public enum NudgeOutcome
{
    /// <summary>The session's own tab could not be identified, so nothing was sent.</summary>
    TabNotConfirmed,

    /// <summary>The terminal was not in front at the moment of sending, so nothing was sent.</summary>
    FocusLost,

    /// <summary>Windows would not accept the input.</summary>
    Refused,

    /// <summary>The keystrokes were sent to the confirmed tab.</summary>
    Sent,
}
