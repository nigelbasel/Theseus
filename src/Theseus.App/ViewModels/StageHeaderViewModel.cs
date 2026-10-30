using System.ComponentModel;

namespace Theseus.App.ViewModels;

/// <summary>
/// The heading of one stage, written once across the top of the board, with the button that folds
/// the stage away in every row. Folded, it still says whether any card in the stage has a session
/// running, so folding a column never hides live work.
/// </summary>
public sealed class StageHeaderViewModel : ObservableObject
{
    private readonly IReadOnlyList<ColumnViewModel> _cells;
    private bool _isCollapsed;

    /// <summary>Creates the heading over the stage's cell in each row.</summary>
    public StageHeaderViewModel(string stage, IReadOnlyList<ColumnViewModel> cells)
    {
        Stage = stage;
        _cells = cells;

        foreach (var cell in cells)
        {
            cell.PropertyChanged += OnCellChanged;
        }
    }

    /// <summary>The stage's name.</summary>
    public string Stage { get; }

    /// <summary>Folds the stage away, or opens it again.</summary>
    public RelayCommand? ToggleCommand { get; set; }

    /// <summary>Whether the stage is folded in every row. Remembered between runs.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (Set(ref _isCollapsed, value))
            {
                foreach (var cell in _cells)
                {
                    cell.IsCollapsed = value;
                }

                Raise(nameof(Glyph));
                Raise(nameof(ToggleTooltip));
            }
        }
    }

    /// <summary>The chevron: pointing left to fold an open stage, right to open a folded one.</summary>
    public string Glyph => IsCollapsed ? "" : "";

    /// <summary>What the fold button says.</summary>
    public string ToggleTooltip => IsCollapsed ? $"Open {Stage}" : $"Fold {Stage} away in every row";

    /// <summary>How many cards in the stage, across every row, have a session running.</summary>
    public int LiveCount => _cells.Sum(cell => cell.LiveCount);

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ColumnViewModel.LiveCount))
        {
            Raise(nameof(LiveCount));
        }
    }
}
