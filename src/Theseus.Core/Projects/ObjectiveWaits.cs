namespace Theseus.Core.Projects;

/// <summary>
/// The items of Waits for you that belong to an objective: those naming it, and those concerning a piece of work serving
/// it. An objective's mark says waiting for you exactly when it has any, so the mark and the list have one source
/// (project-view-usability item 6, Nigel 2026-11-23: a ◆ with nothing to act on).
/// </summary>
public static class ObjectiveWaits
{
    /// <summary>The waits of one objective, in the order Waits for you has them.</summary>
    /// <param name="number">The objective's number.</param>
    /// <param name="serving">The pieces of work serving it.</param>
    /// <param name="waits">Every item of Waits for you.</param>
    public static IReadOnlyList<WaitItem> For(string number, IReadOnlyList<PieceOfWork> serving, IReadOnlyList<WaitItem> waits)
    {
        ArgumentNullException.ThrowIfNull(serving);
        ArgumentNullException.ThrowIfNull(waits);

        var names = serving.Select(piece => piece.Name).ToHashSet(StringComparer.Ordinal);

        return [.. waits.Where(wait => string.Equals(wait.Objective, number, StringComparison.Ordinal) || wait.Pieces.Any(names.Contains))];
    }
}
