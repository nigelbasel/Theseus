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

    /// <summary>A child's share of the width relative to the others: 1 is the even share.</summary>
    public static readonly DependencyProperty WeightProperty = DependencyProperty.RegisterAttached(
        "Weight", typeof(double), typeof(StageColumnsPanel),
        new FrameworkPropertyMetadata(1.0,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure | FrameworkPropertyMetadataOptions.AffectsParentArrange));

    /// <summary>Reads a child's share of the width.</summary>
    public static double GetWeight(DependencyObject element) => (double)element.GetValue(WeightProperty);

    /// <summary>Sets a child's share of the width.</summary>
    public static void SetWeight(DependencyObject element, double value) => element.SetValue(WeightProperty, value);

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
        var weights = Enumerable.Range(0, count)
            .Select(index => GetIsFolded(InternalChildren[index]) ? 0 : Math.Max(0.05, GetWeight(InternalChildren[index])))
            .ToArray();
        var total = weights.Sum();

        if (double.IsInfinity(available) || total <= 0)
        {
            return [.. Enumerable.Range(0, count).Select(index => GetIsFolded(InternalChildren[index]) ? FoldedWidth : double.PositiveInfinity)];
        }

        var open = Math.Max(0, available - (folded * FoldedWidth));

        return [.. Enumerable.Range(0, count).Select(index => GetIsFolded(InternalChildren[index]) ? FoldedWidth : open * weights[index] / total)];
    }
}
