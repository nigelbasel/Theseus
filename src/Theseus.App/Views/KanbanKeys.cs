using System.Windows.Input;

namespace Theseus.App.Views;

/// <summary>The kanban's own keyboard commands (projects-kanban spec AC 13, 23).</summary>
public static class KanbanKeys
{
    /// <summary>Ctrl+Plus: fits every open column to its cards, Explorer's key for the same.</summary>
    public static RoutedUICommand FitAll { get; } = new("Fit every column", nameof(FitAll), typeof(KanbanKeys));
}
