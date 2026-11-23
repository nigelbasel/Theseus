using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Theseus.App.Controls;

/// <summary>
/// A selector bar's keyboard behaviour on its items (checklist G124): an arrow key that moves focus to an item selects it,
/// as WinUI's SelectorBar does, so the arrows switch the view and not only the focus. Only the selected item is a Tab stop
/// (set by <c>SelectorBarItem</c>'s trigger), so Tab lands on it and leaves the bar.
/// </summary>
public static class SelectorBar
{
    /// <summary>Whether an item is selected when the keyboard moves focus to it.</summary>
    public static readonly DependencyProperty SelectsOnFocusProperty = DependencyProperty.RegisterAttached(
        "SelectsOnFocus", typeof(bool), typeof(SelectorBar), new PropertyMetadata(false, OnSelectsOnFocusChanged));

    /// <summary>Gets <see cref="SelectsOnFocusProperty"/>.</summary>
    public static bool GetSelectsOnFocus(DependencyObject element) => (bool)element.GetValue(SelectsOnFocusProperty);

    /// <summary>Sets <see cref="SelectsOnFocusProperty"/>.</summary>
    public static void SetSelectsOnFocus(DependencyObject element, bool value) => element.SetValue(SelectsOnFocusProperty, value);

    private static void OnSelectsOnFocusChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not ToggleButton item)
        {
            return;
        }

        item.GotKeyboardFocus -= Select;

        if ((bool)change.NewValue)
        {
            item.GotKeyboardFocus += Select;
        }
    }

    // Only focus the keyboard moved here: a click selects by itself, and a window's activation restores focus with no key.
    private static void Select(object sender, KeyboardFocusChangedEventArgs focus)
    {
        if (sender is ToggleButton { IsChecked: not true } item && InputManager.Current.MostRecentInputDevice is KeyboardDevice)
        {
            item.IsChecked = true;
            item.Command?.Execute(item.CommandParameter);
        }
    }
}
