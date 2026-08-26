using System.Collections.ObjectModel;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One column of the board.</summary>
public sealed class ColumnViewModel : ObservableObject
{
    /// <summary>Creates a column.</summary>
    public ColumnViewModel(BoardColumn column, string heading)
    {
        Column = column;
        Heading = heading;
        Cards.CollectionChanged += (_, _) => Raise(nameof(Count));
    }

    /// <summary>Which column this is.</summary>
    public BoardColumn Column { get; }

    /// <summary>The column's heading.</summary>
    public string Heading { get; }

    /// <summary>Starts every workstream in this lane that is not already running.</summary>
    public RelayCommand? StartAllCommand { get; set; }

    /// <summary>The cards in this column, in board order.</summary>
    public ObservableCollection<WorkstreamCardViewModel> Cards { get; } = [];

    /// <summary>How many cards the column holds, shown next to the heading.</summary>
    public int Count => Cards.Count;
}
