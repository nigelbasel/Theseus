namespace Theseus.Core.Projects;

/// <summary>Where a session's request to start a card the owner paused stands (sessions-run-the-project item 12).</summary>
public enum StartRequestStatus
{
    /// <summary>Waiting for the owner.</summary>
    Pending,

    /// <summary>The owner allowed it, and the card was started.</summary>
    Allowed,

    /// <summary>The owner refused it; his pause stands.</summary>
    Refused,

    /// <summary>It no longer applies: he started or resumed the card himself, or it left the project or was archived.</summary>
    Lapsed,
}
