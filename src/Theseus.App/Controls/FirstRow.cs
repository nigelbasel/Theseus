using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Theseus.App.Controls;

/// <summary>
/// Puts the focus on a list's starting row when a list window opens (baseline W43, checklist G66): the chosen row of a
/// list that has one, so the arrows move from the row whose content is shown, or else the first. The row itself takes the
/// focus where the list's rows do, as a ListBox's do, or else the first control it holds, such as its check box.
/// </summary>
public static class FirstRow
{
    /// <summary>Focuses the starting row of <paramref name="list"/>; false only when it has no row.</summary>
    public static bool Focus(ItemsControl list)
    {
        ArgumentNullException.ThrowIfNull(list);
        list.UpdateLayout();
        int index = list is Selector { SelectedIndex: >= 0 } chosen ? chosen.SelectedIndex : 0;

        if (list.ItemContainerGenerator.ContainerFromIndex(index) is not UIElement row)
        {
            return false;
        }

        (row.Focusable ? row : FirstFocusable(row))?.Focus();

        return true;
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
