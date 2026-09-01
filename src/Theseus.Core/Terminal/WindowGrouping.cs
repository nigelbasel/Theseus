namespace Theseus.Core.Terminal;

/// <summary>How sessions are spread across terminal windows when they start.</summary>
public enum WindowGrouping
{
    /// <summary>Everything in one window, as tabs. What Theseus has always done.</summary>
    OneWindow,

    /// <summary>
    /// A window for each lane that has sessions, so the desktop mirrors the board and the windows
    /// can be arranged against one another.
    /// </summary>
    WindowPerLane,
}
