namespace Theseus.Core.Projects;

/// <summary>
/// What a project's Start offers (1.1d AC 43; Nigel's answer to question 4, 2026-11-21): each of its cards, as the card
/// of its current piece of work, in file order. A card with no session whose current piece is neither done nor waiting
/// on the owner is ticked; a paused one is unticked, since a pause was deliberate; a running, blocked, done or waiting
/// card is greyed with its reason and never started.
/// </summary>
public sealed class ProjectStart
{
    private ProjectStart(string title, IReadOnlyList<ProjectStartEntry> entries)
    {
        Title = title;
        Entries = entries;
    }

    /// <summary>The project's title.</summary>
    public string Title { get; }

    /// <summary>Its cards, in file order.</summary>
    public IReadOnlyList<ProjectStartEntry> Entries { get; }

    /// <summary>Whether any card can be ticked, so Start is enabled.</summary>
    public bool CanStart => Entries.Any(entry => entry.CanStart);

    /// <summary>What Start does, or why nothing can start ("No card of Theseus bootstrap can start: git-22 is running").</summary>
    public string ToolTip => CanStart
        ? $"Start {Title}: asks first, listing its cards"
        : Entries.Count == 0
            ? $"No card of {Title} is on the board"
            : $"No card of {Title} can start: {string.Join("; ", Entries.Select(entry => $"{entry.Title} {entry.Reason}"))}";

    /// <summary>The Start of a project, from its pieces of work as <see cref="PiecesOfWork.Build"/> gave them.</summary>
    public static ProjectStart For(string title, IReadOnlyList<PieceOfWork> pieces)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(pieces);

        var entries = pieces
            .Where(piece => piece.IsCurrentPiece && piece.Card is not null)
            .DistinctBy(piece => piece.CardKey, StringComparer.OrdinalIgnoreCase)
            .Select(Entry)
            .ToList();

        return new ProjectStart(title, entries);
    }

    private static ProjectStartEntry Entry(PieceOfWork piece)
    {
        var card = piece.Card!;
        var greyed = card switch
        {
            { Lifecycle: Domain.SessionLifecycle.NeedsAttention } => "is waiting for your answer",
            { HasSession: true } => "is running",
            { IsBlocked: true } => "is blocked",
            { CanStart: false } => "cannot be started from the board",
            _ => piece.Status.State switch
            {
                StageNodeState.Done => "is done",
                StageNodeState.WaitingOnYou => "is waiting on you",
                _ => null,
            },
        };

        if (greyed is not null)
        {
            return new ProjectStartEntry(card.Key, card.Title, greyed, CanStart: false, IsTicked: false);
        }

        return card.IsPaused
            ? new ProjectStartEntry(card.Key, card.Title, "is paused", CanStart: true, IsTicked: false)
            : new ProjectStartEntry(card.Key, card.Title, null, CanStart: true, IsTicked: true);
    }
}
