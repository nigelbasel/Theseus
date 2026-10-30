using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Theseus.Core.Domain;

namespace Theseus.App.ViewModels;

/// <summary>
/// One cell of the board: a stage of work within one area. On a board with a single area it is a
/// lane, which is what it used to be, and reads the same.
/// </summary>
public sealed class ColumnViewModel : ObservableObject
{
    private bool _isCollapsed;
    private double _weight = 1;

    /// <summary>Creates a cell.</summary>
    public ColumnViewModel(string column, string area = "")
    {
        Column = column;
        Area = area;
        Cards.CollectionChanged += OnCardsChanged;
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

    /// <summary>How many of its cards have a session running, which a folded column still shows.</summary>
    public int LiveCount => Cards.Count(card => card.IsLive);

    /// <summary>Whether this cell's stage is folded away, across every row.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => Set(ref _isCollapsed, value);
    }

    /// <summary>This cell's share of the row's width, the same as its column heading's.</summary>
    public double Weight
    {
        get => _weight;
        set => Set(ref _weight, value);
    }

    /// <summary>What the folded cell says when hovered.</summary>
    public string FoldedTooltip => LiveCount == 0
        ? $"{Column}: {Count} card(s)"
        : $"{Column}: {Count} card(s), {LiveCount} with a session running";

    private void OnCardsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var card in e.OldItems?.OfType<WorkstreamCardViewModel>() ?? [])
        {
            card.PropertyChanged -= OnCardChanged;
        }

        foreach (var card in e.NewItems?.OfType<WorkstreamCardViewModel>() ?? [])
        {
            card.PropertyChanged += OnCardChanged;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var card in Cards)
            {
                card.PropertyChanged -= OnCardChanged;
                card.PropertyChanged += OnCardChanged;
            }
        }

        Raise(nameof(Count));
        RaiseLive();
    }

    private void OnCardChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkstreamCardViewModel.IsLive))
        {
            RaiseLive();
        }
    }

    private void RaiseLive()
    {
        Raise(nameof(LiveCount));
        Raise(nameof(FoldedTooltip));
    }
}
