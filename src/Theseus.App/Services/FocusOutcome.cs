namespace Theseus.App.Services;

/// <summary>How far a request to bring a session to the front got.</summary>
public enum FocusOutcome
{
    /// <summary>No terminal window could be found for the session.</summary>
    NotFound,

    /// <summary>The window was raised, but its tab could not be identified.</summary>
    WindowOnly,

    /// <summary>The window was raised and the session's own tab selected.</summary>
    TabSelected,
}
