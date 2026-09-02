using System.Collections.ObjectModel;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>
/// One cell of the board: a stage of work within one area. On a board with a single area it is a
/// lane, which is what it used to be, and reads the same.
/// </summary>
public sealed class ColumnViewModel : ObservableObject
{
    /// <summary>Creates a cell.</summary>
    public ColumnViewModel(string column, string area = "")
    {
        Column = column;
        Area = area;
        Cards.CollectionChanged += (_, _) => Raise(nameof(Count));
    }

    /// <summary>Which stage this is, by name.</summary>
    public string Column { get; }

    /// <summary>Which area this cell belongs to, by name.</summary>
    public string Area { get; }

    /// <summary>The lane's heading, which is its name.</summary>
    public string Heading => Column;

    /// <summary>The cards in this column, in board order.</summary>
    public ObservableCollection<WorkstreamCardViewModel> Cards { get; } = [];

    /// <summary>How many cards the column holds, shown next to the heading.</summary>
    public int Count => Cards.Count;
}
