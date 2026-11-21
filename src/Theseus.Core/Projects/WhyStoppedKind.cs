namespace Theseus.Core.Projects;

/// <summary>Which reason a card is stopped for (1.1e AC 12).</summary>
public enum WhyStoppedKind
{
    /// <summary>Its report back was confirmed, or it is in its last stage: its ▶ is Restart for more work.</summary>
    Done,

    /// <summary>In its stopped project's run state: its ▶ starts the project.</summary>
    ProjectStopped,

    /// <summary>Stopped by hand while its project ran: the project does not start it again.</summary>
    StoppedByYou,
}
