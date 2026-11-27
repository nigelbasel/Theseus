using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Theseus.App.Controls;

/// <summary>
/// The right-click menu of a banner's selectable text: Copy and Select all, aimed at the box that opened it (Nigel,
/// 2026-11-27, 1.3e). Each box is given a menu of its own, made in code: a menu set once in a shared style is a single
/// control, owned by the thread that first built the style, and a window built on another thread then cannot load.
/// </summary>
public static class SelectableTextMenu
{
    /// <summary>Gives a TextBox its own Copy and Select all menu.</summary>
    public static readonly DependencyProperty IsOfferedProperty = DependencyProperty.RegisterAttached(
        "IsOffered", typeof(bool), typeof(SelectableTextMenu), new PropertyMetadata(false, OnIsOfferedChanged));

    /// <summary>Gets <see cref="IsOfferedProperty"/>.</summary>
    public static bool GetIsOffered(DependencyObject element) => (bool)element.GetValue(IsOfferedProperty);

    /// <summary>Sets <see cref="IsOfferedProperty"/>.</summary>
    public static void SetIsOffered(DependencyObject element, bool value) => element.SetValue(IsOfferedProperty, value);

    /// <summary>The menu for <paramref name="box"/>: Copy, then Select all, each sent to that box.</summary>
    public static ContextMenu For(TextBox box) => new()
    {
        Items =
        {
            new MenuItem { Header = "_Copy", Command = ApplicationCommands.Copy, CommandTarget = box, InputGestureText = "Ctrl+C" },
            new MenuItem { Header = "Select _all", Command = ApplicationCommands.SelectAll, CommandTarget = box, InputGestureText = "Ctrl+A" },
        },
    };

    private static void OnIsOfferedChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is TextBox box)
        {
            box.ContextMenu = change.NewValue is true ? For(box) : null;
        }
    }
}
