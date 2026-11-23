using System.Collections.ObjectModel;
using System.ComponentModel;

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
    RelayCommand Show) : INotifyPropertyChanged
{
    private string _session = string.Empty;
    private RelayCommand? _focus;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>The cards it hands over to.</summary>
    public ObservableCollection<ChainNodeViewModel> Children { get; } = [];

    /// <summary>
    /// "● running", "○ idle" or "◆ waiting" while it has a session; empty otherwise (item 8). Changed in place, so a
    /// session's every turn does not rebuild the tree and lose its selection and folds.
    /// </summary>
    public string Session
    {
        get => _session;
        set
        {
            if (_session != value)
            {
                _session = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Session)));
            }
        }
    }

    /// <summary>⌖ Focus: brings its tab forward in one click, while it has a session to focus; null otherwise (item 8).</summary>
    public RelayCommand? Focus
    {
        get => _focus;
        set
        {
            if (!ReferenceEquals(_focus, value))
            {
                _focus = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Focus)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFocus)));
            }
        }
    }

    /// <summary>Whether ⌖ Focus shows on the row.</summary>
    public bool HasFocus => _focus is not null;

    /// <summary>Its key, shown when another row has the same title so the two can be told apart; empty otherwise.</summary>
    public string Copy { get; init; } = string.Empty;
}
