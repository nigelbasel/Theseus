using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One project's cards in one column (projects-kanban spec AC 20): waiting for you, stuck, busy and holdouts not run
/// first, then the rest in project-file order. It is as wide as its column.
/// </summary>
public sealed class KanbanCellViewModel
{
    private static readonly StageNodeState[] First =
        [StageNodeState.WaitingOnYou, StageNodeState.Stuck, StageNodeState.Busy, StageNodeState.HoldoutsNotRun];

    internal KanbanCellViewModel(KanbanColumnViewModel column, IEnumerable<KanbanCardViewModel> cards)
    {
        Column = column;
        Cards = Ordered(cards, card => card.State);
    }

    /// <summary>The cell's order: the states that need you first, in <see cref="First"/>'s order, then file order.</summary>
    internal static IReadOnlyList<T> Ordered<T>(IEnumerable<T> items, Func<T, StageNodeState> stateOf) =>
        [.. items.Select((item, index) => (Item: item, Index: index))
            .OrderBy(entry => Rank(stateOf(entry.Item))).ThenBy(entry => entry.Index).Select(entry => entry.Item)];

    /// <summary>Its column, whose width and collapse it follows.</summary>
    public KanbanColumnViewModel Column { get; }

    /// <summary>Its cards, in order.</summary>
    public IReadOnlyList<KanbanCardViewModel> Cards { get; }

    /// <summary>The count a collapsed column or a folded row shows; empty with none.</summary>
    public string CountText => Cards.Count == 0 ? string.Empty : Cards.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>What a screen reader says of it.</summary>
    public string AutomationName => $"{Column.Name}, {Cards.Count} {(Cards.Count == 1 ? "card" : "cards")}";

    private static int Rank(StageNodeState state) => Array.IndexOf(First, state) is var at and >= 0 ? at : First.Length;
}
