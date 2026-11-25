using Theseus.Core.Running;

namespace Theseus.Core.Launching;

/// <summary>
/// A start refused because a card's breaker tripped or a cost limit was reached: the two refusals only the owner can clear,
/// so the board says them where Start was pressed (Nigel, 2026-11-25).
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">The card's title.</param>
/// <param name="Reason">Which: <see cref="RefusalReason.BreakerTripped"/> or <see cref="RefusalReason.BudgetReached"/>.</param>
/// <param name="Detail">The breaker's or the limit's own sentence, with its figures.</param>
public sealed record StartBlock(string Key, string Title, RefusalReason Reason, string Detail);
