using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>One state of the legend (1.1d AC 22): its mark in its colour, then its word.</summary>
/// <param name="State">The state.</param>
public sealed record LegendItemViewModel(StageNodeState State)
{
    /// <summary>The state's mark.</summary>
    public string Mark => StageNodeStates.Mark(State);

    /// <summary>The state's word.</summary>
    public string Word => StageNodeStates.Word(State);
}
