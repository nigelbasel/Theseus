using System.Windows.Input;
using Theseus.Core.Kanban;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// One project's row of the kanban (projects-kanban spec AC 4 to 7): a header fixed on the left with its colour, title,
/// waits and counts, which folds the row and opens the project's tab; and a cell per column.
/// </summary>
public sealed class KanbanRowViewModel : ObservableObject
{
    private readonly Action<KanbanRowViewModel> _changed;
    private bool _isFolded;

    internal KanbanRowViewModel(KanbanProjectSource source, IReadOnlyList<KanbanCellViewModel> cells, bool isFolded,
        Action<KanbanRowViewModel> changed, Action<string> openProject)
    {
        Source = source;
        Cells = cells;
        _isFolded = isFolded;
        _changed = changed;
        ToggleCommand = new RelayCommand(_ => IsFolded = !IsFolded);
        OpenCommand = new RelayCommand(_ => openProject(source.Name), _ => source.View is not null && source.PutAway is null);
    }

    /// <summary>The project as the shell gave it.</summary>
    public KanbanProjectSource Source { get; }

    /// <summary>The project's name.</summary>
    public string Name => Source.Name;

    /// <summary>Its title.</summary>
    public string Title => Source.Title;

    /// <summary>Its colour mark, or the accent's.</summary>
    public string? ColourHex => Source.ColourHex;

    /// <summary>A cell per column, in column order.</summary>
    public IReadOnlyList<KanbanCellViewModel> Cells { get; }

    /// <summary>"◆ 4 waiting", the count its tab's dot stands for; empty with none (AC 5).</summary>
    public string WaitsText => Source.WaitingCount > 0 ? $"◆ {Source.WaitingCount} waiting" : string.Empty;

    /// <summary>Whether its waits show.</summary>
    public bool HasWaits => Source.WaitingCount > 0;

    /// <summary>"12 open · 31 done", or why its file does not read (AC 7).</summary>
    public string CountsText => Source.Problem ?? $"{Open} open · {Done} done";

    /// <summary>"closed" or "archived" when shown on request; empty otherwise (AC 4).</summary>
    public string PutAwayText => Source.PutAway ?? string.Empty;

    /// <summary>Whether it is dimmed: put away, or not readable.</summary>
    public bool IsDimmed => Source.PutAway is not null || Source.View is null;

    /// <summary>Whether its row shows only counts (AC 6).</summary>
    public bool IsFolded
    {
        get => _isFolded;
        set
        {
            if (Set(ref _isFolded, value))
            {
                Raise(nameof(IsUnfolded));
                Raise(nameof(Chevron));
                Raise(nameof(ChevronToolTip));
                Raise(nameof(AutomationName));
                _changed(this);
            }
        }
    }

    /// <summary>Whether its cards show.</summary>
    public bool IsUnfolded => !_isFolded;

    /// <summary>The fold chevron, in the icon font.</summary>
    public string Chevron => FoldGlyph.Of(!_isFolded);

    /// <summary>What the chevron does.</summary>
    public string ChevronToolTip => _isFolded ? $"Unfold {Title}" : $"Fold {Title} to its counts";

    /// <summary>Folds or unfolds it.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>Opens the project's own tab (AC 3).</summary>
    public ICommand OpenCommand { get; }

    /// <summary>What a screen reader says of its header (AC 24).</summary>
    public string AutomationName => string.Join(", ", new[]
    {
        Title, PutAwayText, HasWaits ? $"{Source.WaitingCount} waiting for you" : string.Empty, CountsText,
        _isFolded ? "folded" : string.Empty,
    }.Where(part => part.Length > 0));

    /// <summary>Every card of the row.</summary>
    public IEnumerable<KanbanCardViewModel> Cards => Cells.SelectMany(cell => cell.Cards);

    private int Done => Cells.Where(cell => cell.Column.Column == KanbanColumn.Done).Sum(cell => cell.Cards.Count);

    private int Open => Cards.Count() - Done;
}
