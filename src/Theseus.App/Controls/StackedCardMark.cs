using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Theseus.App.Controls;

/// <summary>
/// The stacked-card mark that says "project" (1.1d AC 17, 19, 38): three offset 14×10 outlines in a 20×16 box, on the
/// 4 epx grid (W36), in the project's colour, or in the secondary text brush until one is given (E1). One element, so the
/// project card, the view's tabs and its summary draw the same mark.
/// </summary>
public sealed class StackedCardMark : Canvas
{
    /// <summary>The outlines' colour.</summary>
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(
        nameof(Foreground), typeof(Brush), typeof(StackedCardMark), new PropertyMetadata(null, (mark, _) => ((StackedCardMark)mark).Stroke()));

    /// <summary>Creates the mark.</summary>
    public StackedCardMark()
    {
        Width = 20;
        Height = 16;
        Focusable = false;
        IsHitTestVisible = false;

        foreach (var (left, top) in new[] { (5.0, 1.0), (3.0, 3.0), (1.0, 5.0) })
        {
            var outline = new Rectangle { Width = 14, Height = 10, RadiusX = 2, RadiusY = 2, StrokeThickness = 1.5 };

            outline.SetResourceReference(Shape.FillProperty, "CardBackgroundFillColorDefaultBrush");
            SetLeft(outline, left);
            SetTop(outline, top);
            Children.Add(outline);
        }

        Stroke();
    }

    /// <summary>The outlines' colour.</summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    // The fallback is a resource reference on the outlines, not a value of Foreground: a local value set here would win
    // over the colour a template binds.
    private void Stroke()
    {
        foreach (Rectangle outline in Children.OfType<Rectangle>())
        {
            if (Foreground is { } colour)
            {
                outline.Stroke = colour;
            }
            else
            {
                outline.SetResourceReference(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
            }
        }
    }
}
