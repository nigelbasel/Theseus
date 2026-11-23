using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// One press of Clear up verifiers… worked through (git-22, 2026-11-23): the running sessions to stop, the reports to
/// reject, and each card archived once its session has gone, since a card with a session cannot be archived.
/// </summary>
public sealed class VerifierClearUp
{
    private readonly List<string> _archived = [];
    private readonly IReadOnlyList<string> _cards;

    private VerifierClearUp(IReadOnlyList<string> cards, IReadOnlyList<string> toStop, IReadOnlyList<string> reportsToReject)
    {
        _cards = cards;
        ToStop = toStop;
        ReportsToReject = reportsToReject;
    }

    /// <summary>The cards whose sessions are stopped first.</summary>
    public IReadOnlyList<string> ToStop { get; }

    /// <summary>The links whose reports are rejected, so none counts as met.</summary>
    public IReadOnlyList<string> ReportsToReject { get; }

    /// <summary>The cards archived so far, in order: what Undo restores.</summary>
    public IReadOnlyList<string> Archived => _archived;

    /// <summary>Whether every card is archived.</summary>
    public bool IsDone => _archived.Count == _cards.Count;

    /// <summary>The clear-up of these cards, given every link.</summary>
    public static VerifierClearUp Plan(IReadOnlyList<WrongVerifier> wrong, IEnumerable<Link> links)
    {
        ArgumentNullException.ThrowIfNull(wrong);
        ArgumentNullException.ThrowIfNull(links);

        var keys = new HashSet<string>(wrong.Select(verifier => verifier.Key), StringComparer.OrdinalIgnoreCase);

        return new VerifierClearUp([.. wrong.Select(verifier => verifier.Key)],
            [.. wrong.Where(verifier => verifier.IsRunning).Select(verifier => verifier.Key)],
            [.. links.Where(link => link.IsActive && keys.Contains(link.From) && link.LatestReport is not null && link.SatisfiedUtc is null)
                .Select(link => link.Id)]);
    }

    /// <summary>The cards to archive now: each not yet archived whose session has gone. Each is given once.</summary>
    /// <param name="isLive">Whether a card's session is still running.</param>
    public IReadOnlyList<string> Due(Func<string, bool> isLive)
    {
        ArgumentNullException.ThrowIfNull(isLive);

        var due = _cards.Where(key => !_archived.Contains(key, StringComparer.OrdinalIgnoreCase) && !isLive(key)).ToList();
        _archived.AddRange(due);

        return due;
    }
}
