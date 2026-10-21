using System.Collections.ObjectModel;

namespace Theseus.App.ViewModels;

/// <summary>
/// One card in the Chains tree. A card sits under the card that spawned it; a waited-on input is a
/// line under the card that waits, never a line drawn between rows, so fan-in needs no special case.
/// </summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="ColorHex">Its colour, as a left border on the row.</param>
/// <param name="State">What its inputs say: "Ready", "waiting", or nothing.</param>
/// <param name="IsReady">Whether it is ready, shown in green.</param>
/// <param name="WaitsOn">What it waits on, with each input's state, or nothing.</param>
/// <param name="Feeds">The cards waiting on its output, or nothing.</param>
/// <param name="Show">Opens the card's details.</param>
public sealed record ChainNodeViewModel(
    string Key,
    string Title,
    string ColorHex,
    string State,
    bool IsReady,
    string WaitsOn,
    string Feeds,
    RelayCommand Show)
{
    /// <summary>The cards it spawned.</summary>
    public ObservableCollection<ChainNodeViewModel> Children { get; } = [];
}
