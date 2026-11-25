using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Theseus.App.Text;

namespace Theseus.App.Controls;

/// <summary>
/// One setting as Options and Project properties draw it (options-made-clear item 6; Nigel, 2026-11-23: "the options are
/// very dense, and having large text boxes is really not great"): its name, its current value as text, and one plain
/// sentence of what it means and does now, with its actions, such as Change…, on the right. Built in code, not from a
/// template, so a window drawn in a test without the application's resources draws it the same.
/// </summary>
public sealed class SettingRow : DockPanel
{
    /// <summary>The setting's name.</summary>
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    /// <summary>Its value now, as text.</summary>
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    /// <summary>What it means and what it does now.</summary>
    public static readonly DependencyProperty ExplanationProperty =
        DependencyProperty.Register(nameof(Explanation), typeof(string), typeof(SettingRow), new PropertyMetadata(string.Empty));

    private readonly StackPanel _text = new();

    /// <summary>Creates the row.</summary>
    public SettingRow()
    {
        Margin = new Thickness(0, 0, 0, 16);
        LastChildFill = true;

        _text.Children.Add(Line(LabelProperty, TypeRamp.Body, "TextFillColorPrimaryBrush", semiBold: true, "Name"));
        _text.Children.Add(Line(ValueProperty, TypeRamp.Caption, "TextFillColorPrimaryBrush", semiBold: false, "Value"));
        _text.Children.Add(Line(ExplanationProperty, TypeRamp.Caption, "TextFillColorSecondaryBrush", semiBold: false, "Explanation"));
        SetBinding(AutomationProperties.NameProperty, new Binding(nameof(Label)) { Source = this });
    }

    /// <summary>The setting's name.</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Its value now, as text.</summary>
    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>What it means and what it does now.</summary>
    public string Explanation
    {
        get => (string)GetValue(ExplanationProperty);
        set => SetValue(ExplanationProperty, value);
    }

    /// <summary>The text lines, for tests and UI Automation readers.</summary>
    public IReadOnlyList<TextBlock> Lines => [.. _text.Children.OfType<TextBlock>()];

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // The actions given in markup sit on the right, top-aligned and spaced; the text fills what is left.
        foreach (var action in Children.OfType<FrameworkElement>().ToList())
        {
            SetDock(action, Dock.Right);
            action.VerticalAlignment = VerticalAlignment.Top;
            action.Margin = new Thickness(8, 0, 0, 0);
        }

        Children.Add(_text);
    }

    private TextBlock Line(DependencyProperty property, double size, string brush, bool semiBold, string part)
    {
        var line = new TextBlock
        {
            FontSize = size,
            TextWrapping = TextWrapping.Wrap,
            FontWeight = semiBold ? FontWeights.SemiBold : FontWeights.Normal,
            Margin = new Thickness(0, semiBold ? 0 : 4, 0, 0),
        };
        line.SetBinding(TextBlock.TextProperty, new Binding(property.Name) { Source = this });
        line.SetResourceReference(TextBlock.ForegroundProperty, brush);
        line.SetBinding(VisibilityProperty, new Binding(property.Name) { Source = this, Converter = HiddenWhenEmpty.Instance });
        AutomationProperties.SetAutomationId(line, part);

        return line;
    }

    private sealed class HiddenWhenEmpty : IValueConverter
    {
        public static HiddenWhenEmpty Instance { get; } = new();

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
