using System.Collections.ObjectModel;

namespace Theseus.App.ViewModels;

/// <summary>
/// One card in the Chains tree, under the card that handed over to it. What it waits on and whom it
/// reports back to are lines beneath it, never lines drawn between rows.
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="ColorHex">Its colour, as a left border on the row.</param>
/// <param name="State">What its inputs say: "Ready", "waiting", or nothing.</param>
/// <param name="IsReady">Whether it is ready, shown in green.</param>
/// <param name="WaitsOn">What it waits on, with each input's state, or nothing.</param>
/// <param name="ReportsBack">The card it reports back to, with the report's state, or nothing.</param>
/// <param name="Show">Opens the card's details.</param>
public sealed record ChainNodeViewModel(
    string Key,
    string Title,
    string ColorHex,
    string State,
    bool IsReady,
    string WaitsOn,
    string ReportsBack,
    RelayCommand Show)
{
    /// <summary>The cards it hands over to.</summary>
    public ObservableCollection<ChainNodeViewModel> Children { get; } = [];

    /// <summary>"● running", "○ idle" or "◆ waiting" while it has a session; empty otherwise (item 8).</summary>
    public string Session { get; init; } = string.Empty;

    /// <summary>⌖ Focus: brings its tab forward in one click, while it has a session; null otherwise (item 8).</summary>
    public RelayCommand? Focus { get; init; }

    /// <summary>Whether ⌖ Focus shows on the row.</summary>
    public bool HasFocus => Focus is not null;

    /// <summary>Its key, shown when another card has the same title so the two can be told apart; empty otherwise.</summary>
    public string Copy { get; init; } = string.Empty;
}
