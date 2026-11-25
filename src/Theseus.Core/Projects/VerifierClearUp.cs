using Theseus.Core.Links;

namespace Theseus.Core.Projects;

/// <summary>
/// One press of Clear up verifiers… worked through (git-22, 2026-11-23): the running sessions to stop, the report-back
/// links to retire so nothing those cards said counts as met, and each card archived once its session has gone, since a
/// card with a session cannot be archived.
/// </summary>
public sealed class VerifierClearUp
{
    private readonly List<string> _archived;
    private readonly IReadOnlyList<string> _cards;
    private readonly List<ClearedRow> _rows;

    private VerifierClearUp(string project, IReadOnlyList<string> cards, IReadOnlyList<string> toStop, IReadOnlyList<Link> before,
        IEnumerable<string> archived, DateTimeOffset atUtc, int round, IEnumerable<ClearedRow> rows)
    {
        Project = project;
        AtUtc = atUtc;
        Round = round;
        _cards = cards;
        ToStop = toStop;
        Before = before;
        _archived = [.. archived];
        _rows = [.. rows];
    }

    /// <summary>The project it clears up.</summary>
    public string Project { get; }

    /// <summary>When it was pressed.</summary>
    public DateTimeOffset AtUtc { get; }

    /// <summary>The project's round when it was pressed.</summary>
    public int Round { get; }

    /// <summary>The cards whose sessions are stopped first.</summary>
    public IReadOnlyList<string> ToStop { get; }

    /// <summary>The cards' report-back links as they were before it, which it retires and Undo puts back.</summary>
    public IReadOnlyList<Link> Before { get; }

    /// <summary>The cards archived so far, in order: what Undo restores.</summary>
    public IReadOnlyList<string> Archived => _archived;

    /// <summary>Whether every card is archived.</summary>
    public bool IsDone => _archived.Count == _cards.Count;

    /// <summary>What Theseus's settings keep of it.</summary>
    public VerifierClearUpRecord Record => new(Project, _cards, [.. _archived], Before) { AtUtc = AtUtc, Round = Round, Rows = [.. _rows] };

    /// <summary>The rows it took out of project.md so far, in the order taken: what Undo puts back.</summary>
    public IReadOnlyList<ClearedRow> Rows => _rows;

    /// <summary>Notes rows it took out of project.md.</summary>
    public void TookRows(IEnumerable<ClearedRow> rows) => _rows.AddRange(rows);

    /// <summary>The clear-up of these cards, given every link.</summary>
    /// <param name="project">The project.</param>
    /// <param name="wrong">The cards to take away.</param>
    /// <param name="links">Every link.</param>
    /// <param name="atUtc">When it is pressed.</param>
    /// <param name="round">The project's round now.</param>
    public static VerifierClearUp Plan(string project, IReadOnlyList<WrongVerifier> wrong, IEnumerable<Link> links,
        DateTimeOffset atUtc = default, int round = 0)
    {
        ArgumentNullException.ThrowIfNull(wrong);
        ArgumentNullException.ThrowIfNull(links);

        var keys = new HashSet<string>(wrong.Select(verifier => verifier.Key), StringComparer.OrdinalIgnoreCase);

        return new VerifierClearUp(project, [.. wrong.Select(verifier => verifier.Key)],
            [.. wrong.Where(verifier => verifier.IsRunning).Select(verifier => verifier.Key)],
            [.. links.Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack && keys.Contains(link.From))], [], atUtc, round, []);
    }

    /// <summary>A clear-up carried on from its record, after a restart.</summary>
    public static VerifierClearUp From(VerifierClearUpRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new VerifierClearUp(record.Project, record.Cards, [], record.Before, record.Archived, record.AtUtc, record.Round, record.Rows ?? []);
    }

    /// <summary>Whether a card is one it takes away, so a second press does not list it again.</summary>
    public bool Covers(string key) => _cards.Contains(key, StringComparer.OrdinalIgnoreCase);

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
