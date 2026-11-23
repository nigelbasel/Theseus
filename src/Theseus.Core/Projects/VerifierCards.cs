using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Which verifier cards still stand for the card they check: one made and never run, or one with a session. A build with
/// one gets no second (Nigel, 2026-11-23: a second press of Start the checks… made a duplicate of each). The project view
/// and the shell both ask this, so they never disagree (review, finding 4).
/// </summary>
public static class VerifierCards
{
    /// <summary>
    /// Whether a card is a verifier that still stands. NotStarted means only "no run today", so one that has run before
    /// and has no session now has finished (review, finding 1).
    /// </summary>
    public static bool Stands(PieceCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card is { Role: WorkstreamRole.Verifier, IsArchived: false }
               && (card.HasSession || (card.Lifecycle == SessionLifecycle.NotStarted && !card.HasRun));
    }

    /// <summary>The standing verifiers by the card each checks: the one its latest active report-back link goes to.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<PieceCard>> BySubject(IEnumerable<PieceCard> cards, IEnumerable<Link> links)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(links);

        var subjectOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack))
        {
            subjectOf[link.From] = link.To;
        }

        return cards.Where(Stands)
            .Select(card => (Card: card, Subject: subjectOf.GetValueOrDefault(card.Key)))
            .Where(entry => entry.Subject is not null)
            .GroupBy(entry => entry.Subject!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PieceCard>)[.. group.Select(entry => entry.Card)],
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Why a card cannot be given another verifier, naming those that stand.</summary>
    public static string WhyAlreadyChecked(IReadOnlyList<string> verifiers)
    {
        ArgumentNullException.ThrowIfNull(verifiers);

        return verifiers.Count == 1
            ? $"{verifiers[0]} is already created to check it: start it rather than make another"
            : $"{string.Join(", ", verifiers)} are already created to check it: start them rather than make another";
    }
}
