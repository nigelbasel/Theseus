namespace Theseus.Core.Budgets;

/// <summary>Whether a run may start, and if so inside which limits.</summary>
/// <param name="Limits">The run's limits, or null when it may not start.</param>
/// <param name="Reached">The limit already reached, when it may not.</param>
/// <param name="Detail">The sentence shown on the card when it may not, naming the limit, the spend and the limit.</param>
public sealed record BudgetVerdict(RunLimits? Limits, BudgetLimit? Reached, string? Detail)
{
    /// <summary>Whether the run may start.</summary>
    public bool Allowed => Limits is not null;
}
