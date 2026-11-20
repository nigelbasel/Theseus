using Theseus.Core.Domain;

namespace Theseus.Core.Terminal;

/// <summary>
/// The session through which a project's terminal window is brought forward (project-view AC 21): a live session of
/// one of its cards whose run was launched into the project's window, as Focus works through a live session today.
/// With none, the reason, for the disabled command's tooltip.
/// </summary>
/// <param name="SessionId">The session to focus, or null when there is none.</param>
/// <param name="Reason">What the command's tooltip says.</param>
public sealed record ProjectWindowTarget(string? SessionId, string Reason)
{
    /// <summary>Finds the session, or why there is none.</summary>
    /// <param name="project">The project's name.</param>
    /// <param name="windowName">The project's window name.</param>
    /// <param name="members">The project's cards: their keys and the sessions they follow, and whether each is live.</param>
    /// <param name="runs">Every run recorded.</param>
    public static ProjectWindowTarget For(string project, string windowName,
        IReadOnlyList<(string Key, string? SessionId, bool IsLive)> members, IReadOnlyList<SessionRun> runs)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(runs);

        var live = members.Where(member => member.IsLive && member.SessionId is not null).ToList();

        if (live.Count == 0)
        {
            return new ProjectWindowTarget(null, $"No session of {project} is running, so its window {windowName} has nothing to bring forward");
        }

        foreach (var member in live)
        {
            // The card's latest run, not its session's: a resume that dies is reminted under a new session id, which
            // the card follows and the run it was launched as does not (PR 3 review, item 1).
            var latest = runs
                .Where(run => string.Equals(run.WorkstreamKey, member.Key, StringComparison.OrdinalIgnoreCase))
                .MaxBy(run => run.StartedUtc);

            if (latest is not null && string.Equals(latest.WindowName, windowName, StringComparison.OrdinalIgnoreCase))
            {
                return new ProjectWindowTarget(member.SessionId, $"Bring {windowName} to the front, through one of its sessions");
            }
        }

        return new ProjectWindowTarget(null,
            $"None of {project}'s running sessions was launched into {windowName}: they started before their card joined "
            + "the project, or in a window of their own. Each moves there at its next start");
    }
}
