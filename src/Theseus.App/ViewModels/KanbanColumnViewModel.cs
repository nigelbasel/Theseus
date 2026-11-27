using System.Windows.Input;
using Theseus.Core.Kanban;

namespace Theseus.App.ViewModels;

/// <summary>
/// One column of the projects kanban (projects-kanban spec AC 8, 11 to 14): its header and count, collapsed or open,
/// resized by its edge and fitted to its content. Every change is remembered through the kanban.
/// </summary>
public sealed class KanbanColumnViewModel : ObservableObject
{
    private readonly Action<KanbanColumnViewModel> _changed;
    private bool _isCollapsed;
    private double _openWidth;

    internal KanbanColumnViewModel(KanbanColumn column, int count, KanbanLayout layout, Action<KanbanColumnViewModel> changed)
    {
        Column = column;
        Count = count;
        _isCollapsed = layout.Collapsed.Contains(column);
        _openWidth = layout.WidthOf(column);
        _changed = changed;
        ToggleCommand = new RelayCommand(_ => IsCollapsed = !IsCollapsed);
    }

    /// <summary>The column.</summary>
    public KanbanColumn Column { get; }

    /// <summary>Its header, with the other pipelines' names for it.</summary>
    public string Header => KanbanColumns.Header(Column);

    /// <summary>Its short name, written upwards when collapsed.</summary>
    public string Name => KanbanColumns.Name(Column);

    /// <summary>How many cards it holds across the rows shown.</summary>
    public int Count { get; }

    /// <summary>Whether it is a narrow strip.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set
        {
            if (Set(ref _isCollapsed, value))
            {
                Raise(nameof(Width));
                Raise(nameof(IsOpen));
                Raise(nameof(AutomationName));
                Raise(nameof(ChevronToolTip));
                _changed(this);
            }
        }
    }

    /// <summary>Whether it is open.</summary>
    public bool IsOpen => !_isCollapsed;

    /// <summary>Its width while open, as last left.</summary>
    public double OpenWidth => _openWidth;

    /// <summary>Its width as drawn: the strip's while collapsed.</summary>
    public double Width => _isCollapsed ? KanbanLayout.CollapsedWidth : _openWidth;

    /// <summary>Collapses or opens it.</summary>
    public ICommand ToggleCommand { get; }

    /// <summary>What the chevron does.</summary>
    public string ChevronToolTip => _isCollapsed ? $"Open the {Name} column" : $"Collapse the {Name} column";

    /// <summary>What a screen reader says (AC 24).</summary>
    public string AutomationName => $"{Header}, {Count} {(Count == 1 ? "card" : "cards")}{(_isCollapsed ? ", collapsed" : string.Empty)}";

    /// <summary>Sets its open width, within the bounds (AC 12), and remembers it; a collapsed column opens at it.</summary>
    public void Resize(double width)
    {
        if (Apply(width))
        {
            _changed(this);
        }
    }

    /// <summary>
    /// Sets its width while its edge is dragged, remembered only by <see cref="EndDrag"/>: a drag moves many times a second,
    /// and each remembering writes the settings file.
    /// </summary>
    public void Drag(double width) => Apply(width);

    /// <summary>Remembers the width a drag left it at.</summary>
    public void EndDrag() => _changed(this);

    private bool Apply(double width)
    {
        var clamped = KanbanLayout.Clamp(width);

        if (clamped == _openWidth && !_isCollapsed)
        {
            return false;
        }

        _openWidth = clamped;
        _isCollapsed = false;
        Raise(nameof(OpenWidth));
        Raise(nameof(Width));
        Raise(nameof(IsCollapsed));
        Raise(nameof(IsOpen));
        Raise(nameof(AutomationName));

        return true;
    }

    /// <summary>Widens or narrows it by this much (Ctrl+Left and Ctrl+Right, AC 23).</summary>
    public void ResizeBy(double delta) => Resize(_openWidth + delta);

    /// <summary>Fits it to its widest card, as measured by the view (AC 13); a column with no card keeps its width.</summary>
    public void Fit(double widestContent)
    {
        if (widestContent > 0)
        {
            Resize(widestContent);
        }
    }
}
