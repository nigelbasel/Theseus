using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Stopping;

/// <summary>
/// Decides which running sessions a stop request reaches. Only sessions a workstream actually
/// follows are included: a session nobody has adopted has no handoff document to be told to write,
/// and stopping something the board does not know about is not a thing the board should do.
/// </summary>
public static class StopSelection
{
    /// <summary>The sessions to stop for one workstream, which is at most one.</summary>
    public static IReadOnlyList<StopTarget> ForWorkstream(
        Workstream workstream,
        SessionRegistrySnapshot live,
        IReadOnlyDictionary<string, string> sessionNames)
    {
        ArgumentNullException.ThrowIfNull(workstream);

        return Targets([workstream], live, sessionNames);
    }

    /// <summary>The sessions to stop across the whole board.</summary>
    public static IReadOnlyList<StopTarget> ForBoard(
        IReadOnlyList<Workstream> workstreams,
        SessionRegistrySnapshot live,
        IReadOnlyDictionary<string, string> sessionNames)
    {
        ArgumentNullException.ThrowIfNull(workstreams);

        return Targets(workstreams, live, sessionNames);
    }

    private static IReadOnlyList<StopTarget> Targets(
        IEnumerable<Workstream> workstreams,
        SessionRegistrySnapshot live,
        IReadOnlyDictionary<string, string> sessionNames)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(sessionNames);

        var targets = new List<StopTarget>();
        var addressed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workstream in workstreams)
        {
            if (string.IsNullOrWhiteSpace(workstream.LastSessionId)
                || live.WithSessionId(workstream.LastSessionId).Count == 0
                || !sessionNames.TryGetValue(workstream.LastSessionId, out var name)
                || string.IsNullOrWhiteSpace(name)
                || !addressed.Add(name))
            {
                continue;
            }

            targets.Add(new StopTarget(name, HandoffPath.For(workstream)));
        }

        return targets;
    }
}
