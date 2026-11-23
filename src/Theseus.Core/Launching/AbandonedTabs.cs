using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Launching;

/// <summary>
/// Which workstreams already have a tab open with nothing running in it.
///
/// Starting one of those opens a second tab beside the first, and the board gives no sign which is
/// which. That happened to Nigel on 2026-09-07: sessions were left in their tabs over a weekend,
/// their Claude processes exited at some point without the tabs closing, and Monday's Start resumed
/// three of them into fresh tabs. Theseus was right that nothing was registered — but wrong to be
/// silent about the tab that was plainly still there.
///
/// A title is all there is to go on. Windows Terminal names a tab after the session, and a tab whose
/// session has gone keeps that name, so a match means "something here is called that". It is not
/// proof: the tab may hold a shell the user renamed, and an elevated terminal cannot be read at all,
/// so this finds what it can, and a Start says what it found rather than asking first (Nigel, 2026-11-23).
/// </summary>
public static class AbandonedTabs
{
    /// <summary>
    /// The workstreams about to start that already have a tab of their own name, with no session
    /// registered for them. Ordered as they were given, so a caller can pair them with its own list.
    /// </summary>
    /// <param name="starting">The workstreams a Start is about to launch.</param>
    /// <param name="tabTitles">Every terminal tab title that could be read.</param>
    /// <param name="live">The sessions running right now.</param>
    /// <param name="occupiedTitles">
    /// The titles of other workstreams launching or running now, each of which owns one tab. Two checks of the same work
    /// share a title, so a tab of that name may be a sibling's (Nigel, 2026-11-23: every verifier Start asked for "Open
    /// new tabs"). A workstream is held only when more tabs carry its name than its siblings account for.
    /// </param>
    public static IReadOnlyList<Workstream> Among(
        IReadOnlyList<Workstream> starting,
        IReadOnlyList<string> tabTitles,
        SessionRegistrySnapshot live,
        IReadOnlyList<string>? occupiedTitles = null)
    {
        ArgumentNullException.ThrowIfNull(starting);
        ArgumentNullException.ThrowIfNull(tabTitles);
        ArgumentNullException.ThrowIfNull(live);

        if (tabTitles.Count == 0)
        {
            return [];
        }

        return
        [
            .. starting.Where(workstream =>
                !IsRunning(workstream, live)
                && Count(tabTitles, workstream.Title) > Count(occupiedTitles ?? [], workstream.Title))
        ];
    }

    private static int Count(IReadOnlyList<string> titles, string title) =>
        titles.Count(candidate => LooseName.Same(candidate, title));

    /// <summary>
    /// Whether a session is registered for this workstream. A workstream whose session is live is not
    /// this problem — it is the one a Start already refuses, and its tab is doing its job.
    /// </summary>
    private static bool IsRunning(Workstream workstream, SessionRegistrySnapshot live) =>
        !string.IsNullOrWhiteSpace(workstream.LastSessionId)
        && live.WithSessionId(workstream.LastSessionId).Count > 0;
}
