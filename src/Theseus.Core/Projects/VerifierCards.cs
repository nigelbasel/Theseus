using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Which verifier cards still stand for the card they check. A build with one gets no second (Nigel, 2026-11-23: a second
/// press of Start the checks… made a duplicate of each). The project view and the shell both ask this, so they never
/// disagree (review, finding 4).
/// </summary>
public static class VerifierCards
{
    /// <summary>
    /// Whether a verifier still stands: from the moment it is made until its report arrives or it is archived, whatever its
    /// session is doing. A session's state cannot decide it: a tab waiting at Claude Code's question about trusting its
    /// folder reads as ended, and a second press then made a copy (review of the auto-start, must fix 1).
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="reportBack">Its latest active report-back link, or null when it has none.</param>
    public static bool Stands(PieceCard card, Link? reportBack)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is { Role: WorkstreamRole.Verifier, IsArchived: false, IsObsolete: false } && reportBack?.LatestReport is null;
    }

    /// <summary>The standing verifiers by the card each checks: the one its latest active report-back link goes to.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PieceCard>> BySubject(IEnumerable<PieceCard> cards, IEnumerable<Link> links)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(links);

        var latest = new Dictionary<string, Link>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack))
        {
            latest[link.From] = link;
        }

        return cards.Select(card => (Card: card, Link: latest.GetValueOrDefault(card.Key)))
            .Where(entry => entry.Link is not null && Stands(entry.Card, entry.Link))
            .GroupBy(entry => entry.Link!.To, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PieceCard>)[.. group.Select(entry => entry.Card)],
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Why a card cannot be given another verifier, naming those that stand.</summary>
    public static string WhyAlreadyChecked(IReadOnlyList<string> verifiers)
    {
        ArgumentNullException.ThrowIfNull(verifiers);

        return verifiers.Count == 1
            ? $"{verifiers[0]} is already made to check it, so no second is made"
            : $"{string.Join(", ", verifiers)} are already made to check it, so no other is made";
    }

    /// <summary>
    /// Whether a standing verifier waits for a start: neither running nor starting, which after a failed start, a lapsed
    /// ticket, a session that exited at once or a restart is the one thing the owner is asked (must fix 2 and 3).
    /// </summary>
    public static bool NeedsAStart(PieceCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return !card.HasSession && !card.IsStarting && !card.IsHeadless;
    }

    /// <summary>The Waits line for a verifier that needs a start, with the reason when it is known.</summary>
    public static string NotRunning(PieceCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var why = card.StartFailure?.Trim().TrimEnd('.');

        // A launcher's refusal names the card itself; said once.
        if (why is not null && why.StartsWith(card.Title, StringComparison.OrdinalIgnoreCase))
        {
            why = why[card.Title.Length..].TrimStart(' ', ':');
        }

        return string.IsNullOrWhiteSpace(why)
            ? $"{card.Title} is not running and has not reported."
            : $"{card.Title} is not running and has not reported: {why}.";
    }
}
