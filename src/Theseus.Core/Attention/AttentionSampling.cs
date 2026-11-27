using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Core.Attention;

/// <summary>
/// Says which workstream a moment belongs to, from what the desktop shows.
///
/// Attended time is attributed by workstream, never by session: two tabs open on one workstream are one
/// workstream's time, which is the duplicate-tab case of 2026-09-07 that would otherwise count twice. Machine time is
/// kept per busy session (7.3): a card whose verifier, headless runs and tab all work at once did that much work, and
/// counting only the card's last session missed most of it (5.9 h seen of 20.5 h of headless runs).
/// </summary>
public static class AttentionSampling
{
    /// <summary>What a terminal tab no workstream is named after is recorded as, with its title after.</summary>
    public const string UnmatchedPrefix = "unmatched: ";

    /// <summary>What time at a project's view in Theseus is recorded as, with the project's name after (7.3).</summary>
    public const string ProjectPrefix = "project: ";

    /// <summary>What time at Theseus with no card or project in view is recorded as, after <see cref="UnmatchedPrefix"/>.</summary>
    public const string TheseusName = "Theseus";

    /// <summary>Attended time seen in a terminal tab.</summary>
    public const string TerminalSource = "terminal";

    /// <summary>Attended time seen in Theseus's own windows (7.3).</summary>
    public const string TheseusSource = "theseus";

    /// <summary>The sample for one instant, as before 7.3: the terminal only, each card's last session only.</summary>
    public static AttentionSample Sample(
        DateTimeOffset at,
        bool terminalInFront,
        string? selectedTab,
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<LiveSession> live) =>
        Sample(at, terminalInFront, selectedTab, workstreams, live, AttentionContext.None);

    /// <summary>The sample for one instant.</summary>
    /// <param name="at">When it was taken.</param>
    /// <param name="terminalInFront">Whether a terminal window was in front, with someone at the keyboard.</param>
    /// <param name="selectedTab">The title of that window's selected tab, when it could be read.</param>
    /// <param name="workstreams">The board's workstreams.</param>
    /// <param name="live">The running Claude Code sessions.</param>
    /// <param name="context">Theseus's own window, the run log's sessions, and what to stamp (7.3).</param>
    public static AttentionSample Sample(
        DateTimeOffset at,
        bool terminalInFront,
        string? selectedTab,
        IReadOnlyList<Workstream> workstreams,
        IReadOnlyList<LiveSession> live,
        AttentionContext context)
    {
        ArgumentNullException.ThrowIfNull(workstreams);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(context);

        var byKey = workstreams.GroupBy(workstream => workstream.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var sessions = BusySessions(workstreams, byKey, live, context);
        var offset = context.Zone is { } zone ? AttentionOffset.Of(at, zone) : null;

        // Without a context the sample is exactly what it was before 7.3, so old callers and old tests see no change.
        var stamped = !ReferenceEquals(context, AttentionContext.None);

        var sample = new AttentionSample(at, null, [.. sessions.Select(session => session.Workstream).Distinct(StringComparer.OrdinalIgnoreCase)])
        {
            BusySessions = stamped ? sessions : [],
            Offset = offset,
        };

        if (terminalInFront)
        {
            var attending = Attending(selectedTab, workstreams);
            var card = byKey.GetValueOrDefault(attending);

            return sample with
            {
                Attending = attending,
                AttendingSession = card?.LastSessionId,
                AttendingStamp = card is null ? null : context.StampOf?.Invoke(card),
                AttendingSource = stamped ? TerminalSource : null,
            };
        }

        if (context.Theseus is { } theseus)
        {
            if (theseus.Card is { } key && byKey.TryGetValue(key, out var card))
            {
                return sample with
                {
                    Attending = card.Key,
                    AttendingSession = card.LastSessionId,
                    AttendingStamp = context.StampOf?.Invoke(card),
                    AttendingSource = TheseusSource,
                };
            }

            if (!string.IsNullOrWhiteSpace(theseus.Project))
            {
                return sample with
                {
                    Attending = ProjectPrefix + theseus.Project.Trim(),
                    AttendingStamp = new AttentionStamp(theseus.Project.Trim(), null, null),
                    AttendingSource = TheseusSource,
                };
            }

            // At Theseus with no card or project in view: kept, like a tab no card is named after, and shown as untracked.
            return sample with { Attending = UnmatchedPrefix + TheseusName, AttendingSource = TheseusSource };
        }

        return sample;
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

    private static List<BusySession> BusySessions(IReadOnlyList<Workstream> workstreams, Dictionary<string, Workstream> byKey,
        IReadOnlyList<LiveSession> live, AttentionContext context)
    {
        var lastSessionOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var workstream in workstreams.Where(workstream => workstream.LastSessionId is not null))
        {
            lastSessionOf.TryAdd(workstream.LastSessionId!, workstream.Key);
        }

        var busy = new List<BusySession>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var session in live.Where(session => string.Equals(session.Status, "busy", StringComparison.OrdinalIgnoreCase)))
        {
            if (!seen.Add(session.SessionId))
            {
                continue;
            }

            var card = lastSessionOf.GetValueOrDefault(session.SessionId) ?? context.SessionCards.GetValueOrDefault(session.SessionId);

            if (card is null)
            {
                continue;
            }

            var stamp = byKey.TryGetValue(card, out var workstream) ? context.StampOf?.Invoke(workstream) : null;
            busy.Add(new BusySession(card, session.SessionId, stamp));
        }

        return busy;
    }
}
