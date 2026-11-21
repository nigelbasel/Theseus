using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Theseus.App.Controls;

/// <summary>
/// The stacked-card mark that says "project" (1.1d AC 17, 19, 38): three offset 14×10 outlines, 18×14 overall, in the
/// project's colour. One element, so the project card, the view's tabs and its summary draw the same mark.
/// </summary>
public sealed class StackedCardMark : Canvas
{
    /// <summary>The outlines' colour.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(StackedCardMark), new PropertyMetadata(Brushes.Gray));

    /// <summary>Creates the mark.</summary>
    public StackedCardMark()
    {
        Width = 18;
        Height = 14;
        Focusable = false;
        IsHitTestVisible = false;

        foreach (var (left, top) in new[] { (4.0, 0.0), (2.0, 2.0), (0.0, 4.0) })
        {
            var outline = new Rectangle { Width = 14, Height = 10, RadiusX = 2, RadiusY = 2, StrokeThickness = 1.5 };

            outline.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = this });
            outline.SetResourceReference(Shape.FillProperty, "CardBackgroundFillColorDefaultBrush");
            SetLeft(outline, left);
            SetTop(outline, top);
            Children.Add(outline);
        }
    }

    /// <summary>The outlines' colour.</summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }
}
