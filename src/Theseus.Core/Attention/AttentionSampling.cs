using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Attention;

/// <summary>
/// Says which workstream a moment belongs to, from what the desktop shows.
///
/// Attention is attributed by workstream, never by session: two tabs open on one workstream are one
/// workstream's time, which is the duplicate-tab case of 2026-09-07 that would otherwise count twice.
/// </summary>
public static class AttentionSampling
{
    /// <summary>What a terminal tab no workstream is named after is recorded as, with its title after.</summary>
    public const string UnmatchedPrefix = "unmatched: ";

    /// <summary>The sample for one instant.</summary>
    /// <param name="at">When it was taken.</param>
    /// <param name="terminalInFront">Whether a terminal window was in front, with someone at the keyboard.</param>
    /// <param name="selectedTab">The title of that window's selected tab, when it could be read.</param>
    /// <param name="workstreams">The board's workstreams.</param>
    /// <param name="live">The running Claude Code sessions.</param>
    public static AttentionSample Sample(
        DateTimeOffset at,
        bool terminalInFront,
        string? selectedTab,
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<LiveSession> live)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(live);

        return new AttentionSample(at, terminalInFront ? Attending(selectedTab, workstreams) : null, Busy(workstreams, live));
    }

    private static string Attending(string? tab, IReadOnlyList<Workstream> workstreams)
    {
        if (string.IsNullOrWhiteSpace(tab))
        {
            return UnmatchedPrefix.TrimEnd();
        }

        return workstreams.FirstOrDefault(workstream =>
                       LooseName.Same(workstream.Title, tab) || LooseName.Same(workstream.Key, tab))
                   ?.Key
               ?? UnmatchedPrefix + tab.Trim();
    }

    private static IReadOnlyList<string> Busy(IReadOnlyList<Workstream> workstreams, IReadOnlyList<LiveSession> live)
    {
        var working = live
            .Where(session => string.Equals(session.Status, "busy", StringComparison.OrdinalIgnoreCase))
            .Select(session => session.SessionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. workstreams
                .Where(workstream => workstream.LastSessionId is { } id && working.Contains(id))
                .Select(workstream => workstream.Key)
                .Distinct(StringComparer.OrdinalIgnoreCase),
        ];
    }
}
