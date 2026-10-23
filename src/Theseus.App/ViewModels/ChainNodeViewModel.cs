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
}
