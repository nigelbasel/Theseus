using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Theseus.App.ViewModels;

/// <summary>
/// One row of the board: an area of work, with a cell for each stage.
///
/// A row can be collapsed, which is the point of having rows at all on a busy board. Collapsed it
/// keeps its header and how many cards each stage holds, so the shape of the work is still legible
/// — where things are stuck, what has not started — without every card being on screen. A row that
/// simply vanished would hide the thing somebody collapsed it to see past.
/// </summary>
public sealed class BoardRowViewModel : ObservableObject
{
    private bool _isCollapsed;

    /// <summary>Creates a row for one area.</summary>
    public BoardRowViewModel(string area)
    {
        Area = area;
        Cells.CollectionChanged += (_, e) =>
        {
            foreach (var cell in e.NewItems?.OfType<ColumnViewModel>() ?? [])
            {
                cell.PropertyChanged += OnCellChanged;
            }

            RaiseCounts();
        };
    }

    /// <summary>Which area this row is, by name.</summary>
    public string Area { get; }

    /// <summary>The cells across the row, one per stage, in stage order.</summary>
    public ObservableCollection<ColumnViewModel> Cells { get; } = [];

    /// <summary>Folds the row away, or opens it again.</summary>
    public RelayCommand? ToggleCommand { get; set; }

    /// <summary>Whether the row is folded away. Remembered between runs.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (Set(ref _isCollapsed, value))
            {
                Raise(nameof(Glyph));
            }
        }
    }

    /// <summary>The chevron on the collapse button: pointing down when open, right when folded.</summary>
    public string Glyph => IsCollapsed ? "" : "";

    /// <summary>How many cards the whole row holds.</summary>
    public int Count => Cells.Sum(cell => cell.Count);

    /// <summary>How many of the row's cards have a session running.</summary>
    public int LiveCount => Cells.Sum(cell => cell.LiveCount);

    /// <summary>What a folded row says about its running sessions, or nothing when none are running.</summary>
    public string LiveText => LiveCount == 0 ? string.Empty : $"{LiveCount} live";

    /// <summary>
    /// What a collapsed row shows instead of its cards: how many are at each stage that has any.
    /// Stages holding nothing are left out — a row of zeroes is noise, and the useful reading is
    /// where the work actually is.
    /// </summary>
    public string Summary => Count == 0
        ? "nothing here"
        : string.Join("   ", Cells.Where(cell => cell.Count > 0)
            .Select(cell => $"{cell.Count} {cell.Column.ToLowerInvariant()}"));

    /// <summary>Says the counts have changed, after cards have been put into the cells.</summary>
    public void RaiseCounts()
    {
        Raise(nameof(Count));
        Raise(nameof(Summary));
        Raise(nameof(LiveCount));
        Raise(nameof(LiveText));
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnViewModel.LiveCount) or nameof(ColumnViewModel.Count))
        {
            RaiseCounts();
        }
    }
}
