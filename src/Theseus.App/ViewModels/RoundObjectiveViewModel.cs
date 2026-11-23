using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One chip of a round's "Set out to meet:" line: an objective's number and title as a link, and a bar of its criteria
/// ticked of total (1.1f C12).
/// </summary>
public sealed class RoundObjectiveViewModel : ObservableObject
{
    private bool _isHighlighted;

    /// <summary>Whether it is highlighted, as the objective of the box hovered or followed (I45).</summary>
    public bool IsHighlighted
    {
        get => _isHighlighted;
        internal set => Set(ref _isHighlighted, value);
    }

    internal RoundObjectiveViewModel(ProjectObjective objective, ICommand? open)
    {
        Objective = objective;
        OpenCommand = open;
    }

    /// <summary>The objective.</summary>
    public ProjectObjective Objective { get; }

    /// <summary>"1.7 GUI parity, table-backed reads".</summary>
    public string Text => $"{Objective.Number} {Objective.Title}";

    /// <summary>"1 of 5".</summary>
    public string CriteriaText => $"{Objective.TickedCount} of {Objective.Criteria.Count} criteria met";

    /// <summary>The bar's filled part, 0 to 1.</summary>
    public double Fraction => Objective.Criteria.Count == 0 ? 0 : (double)Objective.TickedCount / Objective.Criteria.Count;

    /// <summary>The bar's filled width, of 60 px.</summary>
    public double FilledWidth => 60 * Fraction;

    /// <summary>Its hover.</summary>
    public string ToolTip => $"Objective {Objective.Number}: {Objective.Title}. {CriteriaText}.";

    /// <summary>Goes to the objective (I44).</summary>
    public ICommand? OpenCommand { get; }
}
