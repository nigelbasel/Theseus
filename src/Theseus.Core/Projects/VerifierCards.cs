using Theseus.Core.Domain;
using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// Which verifier cards still stand for the build they check: one made and not yet started, or one with a session. A
/// build with one gets no second (Nigel, 2026-11-23: a second press of Start the checks… made a duplicate of each).
/// </summary>
public static class VerifierCards
{
    /// <summary>Whether a verifier card in this state still stands: anything but a session that has ended or vanished.</summary>
    public static bool IsPending(SessionLifecycle lifecycle) => lifecycle is not (SessionLifecycle.Ended or SessionLifecycle.Ghost);

    /// <summary>The card a verifier checks: the one its latest active report-back link goes to, or null.</summary>
    public static string? SubjectOf(string verifierKey, IEnumerable<Link> links) =>
        links.LastOrDefault(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                    && string.Equals(link.From, verifierKey, StringComparison.OrdinalIgnoreCase))?.To;

    /// <summary>Why a build cannot be given another verifier, naming those that stand.</summary>
    public static string WhyAlreadyChecked(IReadOnlyList<string> verifiers)
    {
        ArgumentNullException.ThrowIfNull(verifiers);

        return verifiers.Count == 1
            ? $"{verifiers[0]} is already created to check it: start it rather than make another"
            : $"{string.Join(", ", verifiers)} are already created to check it: start them rather than make another";
    }
}
