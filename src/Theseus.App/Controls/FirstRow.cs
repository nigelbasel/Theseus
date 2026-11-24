using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Theseus.App.Controls;

/// <summary>
/// Puts the focus on a list's first row when a list window opens (baseline W43, checklist G66): the row itself where the
/// list's rows take the focus, as a ListBox's do, or else the first control the row holds, such as its check box.
/// </summary>
public static class FirstRow
{
    /// <summary>Focuses the first row of <paramref name="list"/>; false when it has no row to focus.</summary>
    public static bool Focus(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.UpdateLayout();

        if (list.ItemContainerGenerator.ContainerFromIndex(0) is not UIElement row)
        {
            return false;
        }

        UIElement? target = row.Focusable ? row : FirstFocusable(row);

        return target?.Focus() ?? false;
    }

    private static UIElement? FirstFocusable(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is UIElement { Focusable: true, IsEnabled: true } element)
            {
                return element;
            }

            if (FirstFocusable(child) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }
}
