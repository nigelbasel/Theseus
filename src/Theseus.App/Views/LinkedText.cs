using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Theseus.App.ViewModels;

namespace Theseus.App.Views;

/// <summary>
/// Sets a TextBlock's text with its objective numbers and W-codes as links into the project view (I42). The view model is
/// the enclosing ProjectView's, found when the TextBlock loads, so templates for rounds and waits need nothing extra.
/// </summary>
public static class LinkedText
{
    /// <summary>The text to show, with links.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(LinkedText), new PropertyMetadata(null, (target, _) => Render(target as TextBlock)));

    /// <summary>Gets the text.</summary>
    public static string? GetText(DependencyObject target) => (string?)target.GetValue(TextProperty);

    /// <summary>Sets the text.</summary>
    public static void SetText(DependencyObject target, string? value) => target.SetValue(TextProperty, value);

    private static void Render(TextBlock? block)
    {
        if (block is null)
        {
            return;
        }

        block.Loaded -= OnLoaded;
        block.Loaded += OnLoaded;
        Fill(block);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Fill((TextBlock)sender);

    private static void Fill(TextBlock block)
    {
        var text = GetText(block);
        var view = ViewOf(block);
        block.Inlines.Clear();

        if (view is null)
        {
            block.Text = text ?? string.Empty;
            return;
        }

        foreach (var span in LinkedSpans.Of(text, view.LinkTarget))
        {
            if (span.Follow is { } follow)
            {
                var link = new Hyperlink(new Run(span.Text)) { ToolTip = span.ToolTip, Cursor = System.Windows.Input.Cursors.Hand };
                link.Click += (_, _) => follow();
                System.Windows.Automation.AutomationProperties.SetName(link, span.ToolTip);
                block.Inlines.Add(link);
            }
            else
            {
                block.Inlines.Add(new Run(span.Text));
            }
        }
    }

    private static ProjectViewModel? ViewOf(DependencyObject element)
    {
        for (var at = element; at is not null; at = VisualTreeHelper.GetParent(at) ?? LogicalTreeHelper.GetParent(at))
        {
            if (at is ProjectView { DataContext: ProjectViewModel view })
            {
                return view;
            }
        }

        return null;
    }
}
