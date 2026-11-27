using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Stopping;

/// <summary>
/// When a card whose work is finished is ended without anyone pressing Stop: it is asked to write its handover and exit,
/// as a Stop press asks, and the close step closes its tab once it has. A finished session otherwise sits idle in its tab
/// until somebody closes it by hand.
/// <para>
/// Only an idle session that has been quiet for <see cref="Quiet"/> is ended, with no question of its own unanswered and
/// nothing waiting to be told to it. A verifier or a reporting card is ended only if its last turn ended within
/// <see cref="FinishWindow"/> of its report or of the acceptance, or before them: a turn that ends later is someone
/// talking to it in its tab, or work handed to it, and keeps it open.
/// </para>
/// </summary>
public static class CardEnding
{
    /// <summary>How long a finished session must have been idle before it is ended.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(30);

    /// <summary>How long after its report, or its acceptance, a card's last turn may end and still be the one that finished it.</summary>
    public static readonly TimeSpan FinishWindow = TimeSpan.FromMinutes(10);

    /// <summary>Why this card should be ended now, or null to leave it open.</summary>
    public static CardEndingReason? Decide(CardEndingFacts facts, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.Lifecycle != SessionLifecycle.Idle || facts.SomethingWaits || facts.QuestionOpen || facts.IsLead
            || facts.IdleSinceUtc is not { } idle || now - idle < Quiet)
        {
            return null;
        }

        var since = facts.LastHandInUtc is { } handIn && handIn > facts.SessionStartedUtc ? handIn : facts.SessionStartedUtc;

        return facts.Role switch
        {
            WorkstreamRole.Verifier when ReportsBack(facts).Any(link => !link.ReportIsRejected
                    && link.LatestReport?.AtUtc is { } reported && reported >= facts.SessionStartedUtc && idle <= reported + FinishWindow)
                => CardEndingReason.ReportFiled,
            WorkstreamRole.ScenarioWriter when facts.ScenariosWrittenUtc >= facts.SessionStartedUtc
                => CardEndingReason.ScenariosWritten,
            WorkstreamRole.None when ReportsBack(facts).Any(link => link.SatisfiedUtc is { } accepted && accepted >= since
                    && idle <= accepted + FinishWindow)
                => CardEndingReason.ReportAccepted,
            // Unattended, nobody will talk to it after its report, so it ends once it has reported and written its handoff;
            // the report waits on the board (2.4 W2, "once the report is in, stop").
            WorkstreamRole.None when facts.Unattended && facts.HandoffWrittenUtc >= since
                    && ReportsBack(facts).Any(link => !link.ReportIsRejected && link.LatestReport?.AtUtc is { } reported
                        && reported >= since && idle <= reported + FinishWindow)
                => CardEndingReason.ReportFiled,
            _ => null,
        };
    }

    /// <summary>What the card says of its ending, in words.</summary>
    public static string Text(CardEndingReason reason) => reason switch
    {
        CardEndingReason.ReportFiled => "Ending by itself: its report is filed",
        CardEndingReason.ReportAccepted => "Ending by itself: its report was accepted and nothing more waits for it",
        _ => "Ending by itself: its scenarios are written",
    };

    private static IEnumerable<Link> ReportsBack(CardEndingFacts facts) =>
        facts.Links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                  && string.Equals(link.From, facts.Key, StringComparison.OrdinalIgnoreCase));
}
