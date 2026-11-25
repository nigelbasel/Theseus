namespace Theseus.Core.Budgets;

/// <summary>Which limit a run reached or a start was refused at.</summary>
public enum BudgetLimit
{
    /// <summary>The run's cost.</summary>
    RunCost,

    /// <summary>The run's tokens.</summary>
    RunTokens,

    /// <summary>The run's wall-clock time.</summary>
    RunTime,

    /// <summary>The card's cost today.</summary>
    CardDay,

    /// <summary>The project's cost today.</summary>
    ProjectDay,
}
