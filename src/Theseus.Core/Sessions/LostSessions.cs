using Theseus.Core.Domain;

namespace Theseus.Core.Sessions;

/// <summary>
/// Finds sessions the board has lost track of.
///
/// A workstream follows one session by its identifier, and a session restarted outside Theseus gets
/// a new one. The card then reads as finished and goes on doing so indefinitely, which is
/// indistinguishable from a workstream whose work is genuinely done — the one case where a card is
/// wrong rather than merely uninformative.
///
/// The tell is the name. Theseus names each session after its workstream, so a running session
/// bearing a workstream's name that the workstream is not following has almost certainly been
/// restarted out from under it.
/// </summary>
public static class LostSessions
{
    /// <summary>
    /// The workstreams whose session is running unattached. Nothing is reattached automatically:
    /// binding a workstream to the wrong session is worse than leaving it plainly unbound, so an
    /// ambiguous name yields no match at all.
    /// </summary>
    public static IReadOnlyList<LostSession> Find(
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<LiveSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(sessions);

        var followed = workstreams
            .Select(workstream => workstream.LastSessionId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = workstreams
            .Where(workstream => !workstream.IsArchived)
            .Where(workstream => !IsFollowingSomethingLive(workstream, sessions))
            .ToList();

        return
        [
            .. sessions
                .Where(session => session.IsAdoptable && !followed.Contains(session.SessionId))
                .Select(session => Pair(session, candidates))
                .OfType<LostSession>()
        ];
    }

    private static LostSession? Pair(LiveSession session, IReadOnlyList<Workstream> candidates)
    {
        if (string.IsNullOrWhiteSpace(session.Name))
        {
            return null;
        }

        var named = candidates
            .Where(workstream => LooseName.Same(workstream.Title, session.Name))
            .ToList();

        return named.Count == 1
            ? new LostSession(named[0].Key, session.SessionId, session.Name)
            : null;
    }

    private static bool IsFollowingSomethingLive(Workstream workstream, IReadOnlyList<LiveSession> sessions) =>
        !string.IsNullOrWhiteSpace(workstream.LastSessionId)
        && sessions.Any(session =>
            string.Equals(session.SessionId, workstream.LastSessionId, StringComparison.OrdinalIgnoreCase));
}
