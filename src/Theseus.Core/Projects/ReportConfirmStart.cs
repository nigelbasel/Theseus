using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// Whether confirming a report to a stopped card also starts it (sessions-run-the-project item 16a; Nigel, 2026-11-22):
/// only in a project, so the pipeline does not wait on the owner twice; never a card he paused, blocked or archived. A
/// session's pause does not hold back his gate. The ordinary board only tells.
/// </summary>
public static class ReportConfirmStart
{
    /// <summary>Whether the card the report went to is started with the confirmation.</summary>
    public static bool StartsParent(Workstream parent, bool isLive)
    {
        ArgumentNullException.ThrowIfNull(parent);

        return !isLive
               && parent is { Project: not null, IsArchived: false, IsBlocked: false }
               && !(parent.IsPaused && parent.PausedBy is null);
    }
}
