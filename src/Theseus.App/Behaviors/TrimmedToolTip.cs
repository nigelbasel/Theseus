using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Theseus.App.Behaviors;

/// <summary>
/// A tooltip that shows a TextBlock's whole text only when the text is cut short (baseline W35, checklist G154): a
/// tooltip that repeats what is already on the screen is noise. Set <c>behaviors:TrimmedToolTip.IsEnabled="True"</c> on a
/// TextBlock with TextTrimming; its ToolTip, if it has none of its own, becomes its text.
/// </summary>
public static class TrimmedToolTip
{
    /// <summary>Whether the TextBlock's tooltip opens only when its text is trimmed.</summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(TrimmedToolTip), new PropertyMetadata(false, OnIsEnabledChanged));

    /// <summary>Gets <see cref="IsEnabledProperty"/>.</summary>
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    /// <summary>Sets <see cref="IsEnabledProperty"/>.</summary>
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    /// <summary>Whether the block's text, laid out at its present size, is longer than what it shows.</summary>
    public static bool IsTrimmed(TextBlock block)
    {
        if (string.IsNullOrEmpty(block.Text) || block.ActualWidth <= 0)
        {
            return false;
        }

        double width = Math.Max(0, block.ActualWidth - block.Padding.Left - block.Padding.Right);
        FormattedText text = new(block.Text, CultureInfo.CurrentUICulture, block.FlowDirection,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch), block.FontSize, Brushes.Black,
            VisualTreeHelper.GetDpi(block).PixelsPerDip);

        if (block.TextWrapping == TextWrapping.NoWrap)
        {
            return text.WidthIncludingTrailingWhitespace > width + 0.5;
        }

        text.MaxTextWidth = Math.Max(1, width);

        return text.Height > block.ActualHeight - block.Padding.Top - block.Padding.Bottom + 0.5;
    }

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs change)
    {
        if (element is not TextBlock block)
        {
            return;
        }

        block.ToolTipOpening -= OnToolTipOpening;

        if (change.NewValue is not true)
        {
            return;
        }

        block.ToolTipOpening += OnToolTipOpening;

        if (block.ReadLocalValue(FrameworkElement.ToolTipProperty) == DependencyProperty.UnsetValue)
        {
            block.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(TextBlock.Text)) { Source = block });
        }
    }

    // Handled means "do not open": the tooltip stays shut while the whole text is in view.
    private static void OnToolTipOpening(object sender, ToolTipEventArgs opening) => opening.Handled = !IsTrimmed((TextBlock)sender);
}
