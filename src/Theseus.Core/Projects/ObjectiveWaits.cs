namespace Theseus.Core.Projects;

/// <summary>
/// The items of Waits for you that belong to an objective: those naming it, and those concerning a piece of work serving
/// it. An objective's mark says waiting for you exactly when it has any, so the mark and the list have one source
/// (project-view-usability item 6, Nigel 2026-11-23: a ◆ with nothing to act on).
/// </summary>
public static class ObjectiveWaits
{
    /// <summary>The waits of one objective, in the order Waits for you has them.</summary>
    /// <param name="number">The objective's number, or null for the pieces no project file lists.</param>
    /// <param name="serving">The pieces of work serving it.</param>
    /// <param name="waits">Every item of Waits for you.</param>
    public static IReadOnlyList<WaitItem> For(string? number, IReadOnlyList<PieceOfWork> serving, IReadOnlyList<WaitItem> waits)
    {
        ArgumentNullException.ThrowIfNull(waits);

        var belongs = Belongs(number, serving);

        return [.. waits.Where(belongs)];
    }

    /// <summary>
    /// Whether an item belongs to the objective: it names the objective, concerns a piece serving it, or is a session's
    /// request to start a card serving it.
    /// </summary>
    public static Func<WaitItem, bool> Belongs(string? number, IReadOnlyList<PieceOfWork> serving)
    {
        ArgumentNullException.ThrowIfNull(serving);

        var names = serving.Select(piece => piece.Name).ToHashSet(StringComparer.Ordinal);
        var cards = serving.Where(piece => piece.IsCurrentPiece).Select(piece => piece.CardKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return wait => (number is not null && string.Equals(wait.Objective, number, StringComparison.Ordinal))
                       || wait.Pieces.Any(names.Contains)
                       || (wait.Kind == WaitKind.StartRequest && wait.CardKey is { } card && cards.Contains(card));
    }
}
