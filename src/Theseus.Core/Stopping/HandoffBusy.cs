using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a card's handover file must be left alone because a session of it holds the path. A session being started
/// counts: its opening prompt already names the file, but until it registers the board sees nothing running, so a run
/// that has not ended, or that began within the launch window, counts as busy (handoff-names AC 3).
/// </summary>
public static class HandoffBusy
{
    /// <summary>How long a launch can take to register; the launch ticket's own lifetime.</summary>
    public static readonly TimeSpan LaunchWindow = TimeSpan.FromMinutes(2);

    /// <summary>Whether the card is busy.</summary>
    public static bool Of(string key, bool isLive, IEnumerable<SessionRun> runs, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(runs);

        return isLive || runs.Any(run => string.Equals(run.WorkstreamKey, key, StringComparison.OrdinalIgnoreCase)
                                         && (!run.HasEnded || nowUtc - run.StartedUtc < LaunchWindow));
    }
}
