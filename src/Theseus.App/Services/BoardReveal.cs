using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Theseus.App.Services;

/// <summary>
/// Scrolls a card into view on the board (card-search-and-tags item 6): Enter and F3 select a match, and a match off
/// screen, or in a cell's virtualised list, is no use selected. A card in a folded row has no element, so the first of
/// the items given that has one is shown: the card, else its row.
/// </summary>
public static class BoardReveal
{
    /// <summary>Brings the first of <paramref name="items"/> that the board draws into view; false when none is drawn.</summary>
    public static bool Show(DependencyObject root, params object?[] items)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(items);

        foreach (var item in items.OfType<object>())
        {
            if (ContainerOf(root, item) is { } container)
            {
                container.BringIntoView();

                return true;
            }
        }

        return false;
    }

    private static FrameworkElement? ContainerOf(DependencyObject root, object item)
    {
        foreach (var list in Descendants<ItemsControl>(root))
        {
            if (list.ItemsSource is not IList source || !source.Contains(item) || !list.IsVisible)
            {
                continue;
            }

            if (list.ItemContainerGenerator.ContainerFromItem(item) is FrameworkElement drawn)
            {
                return drawn;
            }

            if (Descendants<VirtualizingStackPanel>(list).FirstOrDefault() is { } panel)
            {
                panel.BringIndexIntoViewPublic(source.IndexOf(item));
                list.UpdateLayout();

                return list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            }
        }

        return null;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
