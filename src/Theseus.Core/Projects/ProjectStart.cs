namespace Theseus.Core.Projects;

/// <summary>
/// What a project's Start starts (1.1e AC 9; Nigel, 2026-11-21: "the decision about what should be running for a
/// particular project is owned by the project itself"), with no list to tick: a stopped project's run state, the cards
/// that had a session when it was stopped; a project never stopped, its first card, the card of the first piece of work
/// not done, else of the file's first row. A card that cannot start now (gone, archived, already running) is skipped and
/// named. A paused or blocked card is started as a card's own Start starts it, which asks first (rubric R13).
/// </summary>
public sealed class ProjectStart
{
    private ProjectStart(string title, bool isStopped, IReadOnlyList<ProjectStartEntry> entries)
    {
        Title = title;
        IsStopped = isStopped;
        Entries = entries;
    }

    /// <summary>The project's title.</summary>
    public string Title { get; }

    /// <summary>Whether the project was stopped, so Start brings back its run state.</summary>
    public bool IsStopped { get; }

    /// <summary>The cards Start would start, and those it would skip, in order.</summary>
    public IReadOnlyList<ProjectStartEntry> Entries { get; }

    /// <summary>The keys of the cards it starts.</summary>
    public IReadOnlyList<string> Keys => [.. Entries.Where(entry => entry.Starts).Select(entry => entry.Key)];

    /// <summary>The cards it skips, each as "title reason".</summary>
    public IReadOnlyList<string> Skipped => [.. Entries.Where(entry => !entry.Starts).Select(entry => entry.Line)];

    /// <summary>Whether anything would start, so Start is enabled.</summary>
    public bool CanStart => Entries.Any(entry => entry.Starts);

    /// <summary>What Start does, or why nothing can start.</summary>
    public string ToolTip => CanStart
        ? $"Start {Title}{(IsStopped ? " as it was when stopped" : string.Empty)}: {string.Join(", ", Entries.Where(entry => entry.Starts).Select(entry => entry.Title))}"
        : Entries.Count == 0
            ? $"No card of {Title} is on the board"
            : $"No card of {Title} can start: {string.Join("; ", Skipped)}";

    /// <summary>The Start of a project, from its pieces of work as <see cref="PiecesOfWork.Build"/> gave them.</summary>
    /// <param name="title">The project's title.</param>
    /// <param name="pieces">Its pieces of work.</param>
    /// <param name="runState">Its run state when it was stopped, or null when it is not stopped.</param>
    /// <param name="stoppedByOwner">The cards the owner stopped by hand, which a project never stopped does not start.</param>
    /// <param name="lead">
    /// The project's lead card: with no run state it is what Start starts, and it decides what else runs (Nigel,
    /// 2026-11-22, replacing 1.1e's first unfinished piece's card). Null starts the first unfinished piece's card.
    /// </param>
    public static ProjectStart For(string title, IReadOnlyList<PieceOfWork> pieces, IReadOnlyList<string>? runState = null,
        IReadOnlyCollection<string>? stoppedByOwner = null, string? lead = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(pieces);

        var cards = pieces.Where(piece => piece.Card is not null).Select(piece => piece.Card!)
            .DistinctBy(card => card.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(card => card.Key, StringComparer.OrdinalIgnoreCase);

        if (runState is not null)
        {
            return new ProjectStart(title, true,
                [.. runState.Distinct(StringComparer.OrdinalIgnoreCase).Select(key => Entry(key, cards.GetValueOrDefault(key), null))]);
        }

        if (lead is not null)
        {
            return new ProjectStart(title, false, [Entry(lead, cards.GetValueOrDefault(lead), stoppedByOwner)]);
        }

        var first = pieces.FirstOrDefault(piece => piece.IsListed && piece.Card is not null && piece.Status.State != StageNodeState.Done)
                    ?? pieces.FirstOrDefault(piece => piece.IsListed && piece.Card is not null);

        return first is null
            ? new ProjectStart(title, false, [])
            : new ProjectStart(title, false, [Entry(first.CardKey, first.Card, stoppedByOwner)]);
    }

    private static ProjectStartEntry Entry(string key, PieceCard? card, IReadOnlyCollection<string>? stoppedByOwner)
    {
        var reason = card switch
        {
            null => "is no longer on the board",
            { HasSession: true } => "is already running",
            { CanStart: false } => "cannot be started from the board",
            { PausedBy: { } by } => $"is paused by {by}",
            _ when stoppedByOwner?.Contains(key, StringComparer.OrdinalIgnoreCase) == true => "was stopped by you",
            _ => null,
        };

        return new ProjectStartEntry(key, card?.Title ?? key, reason);
    }
}
