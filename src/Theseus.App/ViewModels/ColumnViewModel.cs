using System.Collections.ObjectModel;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>One lane of the board.</summary>
public sealed class ColumnViewModel : ObservableObject
{
    /// <summary>Creates a column.</summary>
    public ColumnViewModel(string column)
    {
        Column = column;
        Cards.CollectionChanged += (_, _) => Raise(nameof(Count));
    }

    /// <summary>Which lane this is, by name.</summary>
    public string Column { get; }

    /// <summary>The lane's heading, which is its name.</summary>
    public string Heading => Column;

    /// <summary>Starts every workstream in this lane that is not already running.</summary>
    public RelayCommand? StartAllCommand { get; set; }

    /// <summary>The cards in this column, in board order.</summary>
    public ObservableCollection<WorkstreamCardViewModel> Cards { get; } = [];

    /// <summary>How many cards the column holds, shown next to the heading.</summary>
    public int Count => Cards.Count;
}
