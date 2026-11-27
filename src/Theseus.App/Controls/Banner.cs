using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Theseus.App.Services;

namespace Theseus.App.Controls;

/// <summary>
/// A banner's text can be copied whole (Nigel, 2026-11-27: "the banners should be selectable and copie able so that text
/// can be pasted elsewhere"). Mark the banner with <c>controls:Banner.IsBanner="True"</c>, draw its text with the
/// <c>SelectableText</c> style, and give it a button in the <c>BannerCopyButton</c> style: pressing it puts every line
/// the banner shows on the clipboard, as plain text, one line each.
/// </summary>
public static class Banner
{
    /// <summary>Marks the element whose text a Copy button inside it copies.</summary>
    public static readonly DependencyProperty IsBannerProperty = DependencyProperty.RegisterAttached(
        "IsBanner", typeof(bool), typeof(Banner), new PropertyMetadata(false));

    /// <summary>Makes a button copy the text of the banner it sits in when pressed.</summary>
    public static readonly DependencyProperty CopiesProperty = DependencyProperty.RegisterAttached(
        "Copies", typeof(bool), typeof(Banner), new PropertyMetadata(false, OnCopiesChanged));

    /// <summary>Gets <see cref="IsBannerProperty"/>.</summary>
    public static bool GetIsBanner(DependencyObject element) => (bool)element.GetValue(IsBannerProperty);

    /// <summary>Sets <see cref="IsBannerProperty"/>.</summary>
    public static void SetIsBanner(DependencyObject element, bool value) => element.SetValue(IsBannerProperty, value);

    /// <summary>Gets <see cref="CopiesProperty"/>.</summary>
    public static bool GetCopies(DependencyObject element) => (bool)element.GetValue(CopiesProperty);

    /// <summary>Sets <see cref="CopiesProperty"/>.</summary>
    public static void SetCopies(DependencyObject element, bool value) => element.SetValue(CopiesProperty, value);

    /// <summary>
    /// The banner's text as shown: each non-empty text box it holds, in reading order, one per line, without the spaces at
    /// its ends. A line hidden by its own or a parent's Visibility is not shown, so it is not copied either.
    /// </summary>
    public static string TextOf(DependencyObject banner)
    {
        ArgumentNullException.ThrowIfNull(banner);

        return string.Join(Environment.NewLine, ShownTextBoxes(banner).Select(box => box.Text.Trim()).Where(text => text.Length > 0));
    }

    /// <summary>The nearest banner that holds <paramref name="element"/>, or none.</summary>
    public static DependencyObject? Around(DependencyObject element)
    {
        for (DependencyObject? at = element; at is not null; at = Parent(at))
        {
            if (GetIsBanner(at))
            {
                return at;
            }
        }

        return null;
    }

    private static void OnCopiesChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not ButtonBase button)
        {
            return;
        }

        button.Click -= OnCopyClick;

        if (change.NewValue is true)
        {
            button.Click += OnCopyClick;
        }
    }

    private static void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (Around((DependencyObject)sender) is { } banner)
        {
            ClipboardText.TryCopy(TextOf(banner));
        }
    }

    private static IEnumerable<TextBox> ShownTextBoxes(DependencyObject at)
    {
        if (at is UIElement { Visibility: not Visibility.Visible })
        {
            yield break;
        }

        if (at is TextBox box)
        {
            yield return box;

            yield break;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(at); index++)
        {
            foreach (TextBox inner in ShownTextBoxes(VisualTreeHelper.GetChild(at, index)))
            {
                yield return inner;
            }
        }
    }

    // A popup's content has no visual parent of its own; its logical parent is the popup, which is in the window.
    private static DependencyObject? Parent(DependencyObject element) =>
        (element is Visual ? VisualTreeHelper.GetParent(element) : null) ?? LogicalTreeHelper.GetParent(element);
}
