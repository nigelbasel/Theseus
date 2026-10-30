using System.Windows;
using System.Windows.Controls;

namespace Theseus.App.Controls;

/// <summary>
/// Lays the board's stages out side by side: a folded stage takes a narrow strip, and the open ones
/// share what is left equally. It replaced a UniformGrid, which gives every column the same width
/// and so cannot fold one. The headings and every row use it, so they stay lined up.
/// </summary>
public sealed class StageColumnsPanel : Panel
{
    /// <summary>The width a folded stage keeps: enough for its chevron, count and live mark.</summary>
    public const double FoldedWidth = 44;

    /// <summary>Whether the child is a folded stage.</summary>
    public static readonly DependencyProperty IsFoldedProperty = DependencyProperty.RegisterAttached(
        "IsFolded", typeof(bool), typeof(StageColumnsPanel),
        new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    /// <summary>Reads whether a child is folded.</summary>
    public static bool GetIsFolded(DependencyObject element) => (bool)element.GetValue(IsFoldedProperty);

    /// <summary>Marks a child as folded or open.</summary>
    public static void SetIsFolded(DependencyObject element, bool value) => element.SetValue(IsFoldedProperty, value);

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        var widths = Widths(availableSize.Width);
        var height = 0.0;
        var total = 0.0;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];

            child.Measure(new Size(widths[index], availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
            total += double.IsInfinity(widths[index]) ? child.DesiredSize.Width : widths[index];
        }

        return new Size(total, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var widths = Widths(finalSize.Width);
        var left = 0.0;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            var width = double.IsInfinity(widths[index]) ? child.DesiredSize.Width : widths[index];

            child.Arrange(new Rect(left, 0, width, finalSize.Height));
            left += width;
        }

        return finalSize;
    }

    private double[] Widths(double available)
    {
        var count = InternalChildren.Count;
        var folded = Enumerable.Range(0, count).Count(index => GetIsFolded(InternalChildren[index]));
        var open = count - folded;
        var share = double.IsInfinity(available) || open == 0
            ? double.PositiveInfinity
            : Math.Max(0, (available - (folded * FoldedWidth)) / open);

        return [.. Enumerable.Range(0, count).Select(index => GetIsFolded(InternalChildren[index]) ? FoldedWidth : share)];
    }
}
