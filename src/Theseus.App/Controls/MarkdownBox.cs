using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Theseus.App.Markdown;
using Theseus.Core.Markdown;

namespace Theseus.App.Controls;

/// <summary>
/// A text box for Markdown (1.1e AC 22): a toolbar that only inserts Markdown, Ctrl+B, Ctrl+I and Ctrl+K, and Write |
/// Preview above it, the preview drawn natively (AC 23). The text saved is exactly what is typed.
/// </summary>
public sealed class MarkdownBox : DockPanel
{
    /// <summary>The text, bound two ways and updated as it is typed.</summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(MarkdownBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged)
        {
            DefaultUpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        });

    /// <summary>Whether it can be typed in; read-only, the toolbar is disabled.</summary>
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool),
        typeof(MarkdownBox), new PropertyMetadata(false, (box, _) => ((MarkdownBox)box).ApplyReadOnly()));

    /// <summary>Why it cannot be typed in, for the disabled toolbar's tooltip (1.1e AC 6a).</summary>
    public static readonly DependencyProperty ReadOnlyReasonProperty = DependencyProperty.Register(nameof(ReadOnlyReason), typeof(string),
        typeof(MarkdownBox), new PropertyMetadata("This text cannot be changed now", (box, _) => ((MarkdownBox)box).ApplyReadOnly()));

    /// <summary>The least height of the text and the preview.</summary>
    public static readonly DependencyProperty EditorMinHeightProperty = DependencyProperty.Register(nameof(EditorMinHeight), typeof(double),
        typeof(MarkdownBox), new PropertyMetadata(90d));

    /// <summary>The most height of the text and the preview before they scroll.</summary>
    public static readonly DependencyProperty EditorMaxHeightProperty = DependencyProperty.Register(nameof(EditorMaxHeight), typeof(double),
        typeof(MarkdownBox), new PropertyMetadata(double.PositiveInfinity));

    private readonly List<ButtonBase> _tools = [];
    private bool _isPreviewing;
    private readonly Button _write;
    private readonly Button _preview;
    private readonly FlowDocumentScrollViewer _viewer;

    /// <summary>Builds the box, its toolbar and its preview.</summary>
    public MarkdownBox()
    {
        Editor = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = false,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = MarkdownRenderer.Mono,
            FontSize = 12,
        };
        Editor.SetBinding(TextBox.TextProperty, new Binding(nameof(Text)) { Source = this, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        Editor.SetBinding(MinHeightProperty, new Binding(nameof(EditorMinHeight)) { Source = this });
        Editor.SetBinding(MaxHeightProperty, new Binding(nameof(EditorMaxHeight)) { Source = this });
        Editor.PreviewKeyDown += (_, e) => e.Handled = HandleShortcut(e.Key == Key.System ? e.SystemKey : e.Key, Keyboard.Modifiers);
        AutomationProperties.SetName(Editor, "Markdown text");

        _viewer = new FlowDocumentScrollViewer
        {
            Visibility = Visibility.Collapsed,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsToolBarVisible = false,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4),
        };
        _viewer.SetResourceReference(Control.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
        _viewer.SetBinding(MinHeightProperty, new Binding(nameof(EditorMinHeight)) { Source = this });
        _viewer.SetBinding(MaxHeightProperty, new Binding(nameof(EditorMaxHeight)) { Source = this });
        AutomationProperties.SetName(_viewer, "Markdown preview");

        var bar = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = false };
        _write = Tab("Write", "Write the Markdown", () => IsPreviewing = false);
        _preview = Tab("Preview", "See it drawn, as it will read", () => IsPreviewing = true);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        tabs.Children.Add(_write);
        tabs.Children.Add(new TextBlock { Text = "|", Margin = new Thickness(4, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, Opacity = 0.6 });
        tabs.Children.Add(_preview);
        SetDock(tabs, Dock.Right);
        bar.Children.Add(tabs);

        var tools = new StackPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(Tool(MarkdownAction.Bold, "B", "Bold (Ctrl+B)", text: true, weight: FontWeights.Bold));
        tools.Children.Add(Tool(MarkdownAction.Italic, "I", "Italic (Ctrl+I)", text: true, style: FontStyles.Italic));
        tools.Children.Add(Tool(MarkdownAction.Heading, "H", "Heading", text: true));
        tools.Children.Add(Tool(MarkdownAction.BulletedList, "", "Bulleted list"));
        tools.Children.Add(Tool(MarkdownAction.NumberedList, "1.", "Numbered list", text: true));
        tools.Children.Add(Tool(MarkdownAction.CheckBox, "", "Check box"));
        tools.Children.Add(Tool(MarkdownAction.Code, "", "Code: inline on one line, a block over several"));
        tools.Children.Add(Tool(MarkdownAction.Link, "", "Link (Ctrl+K)"));
        SetDock(tools, Dock.Left);
        bar.Children.Add(tools);

        SetDock(bar, Dock.Top);
        Children.Add(bar);
        var body = new Grid();
        body.Children.Add(Editor);
        body.Children.Add(_viewer);
        Children.Add(body);
        ShowMode();
    }

    /// <summary>The text box itself.</summary>
    public TextBox Editor { get; }

    /// <summary>The text.</summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether it can be typed in.</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Why it cannot be typed in.</summary>
    public string ReadOnlyReason
    {
        get => (string)GetValue(ReadOnlyReasonProperty);
        set => SetValue(ReadOnlyReasonProperty, value);
    }

    /// <summary>The least height of the text and the preview.</summary>
    public double EditorMinHeight
    {
        get => (double)GetValue(EditorMinHeightProperty);
        set => SetValue(EditorMinHeightProperty, value);
    }

    /// <summary>The most height before they scroll.</summary>
    public double EditorMaxHeight
    {
        get => (double)GetValue(EditorMaxHeightProperty);
        set => SetValue(EditorMaxHeightProperty, value);
    }

    /// <summary>Whether the preview is shown instead of the text.</summary>
    public bool IsPreviewing
    {
        get => _isPreviewing;
        set
        {
            _isPreviewing = value;
            ShowMode();
        }
    }

    /// <summary>The preview's document, drawn from the text.</summary>
    public System.Windows.Documents.FlowDocument? PreviewDocument => _viewer.Document;

    /// <summary>The toolbar's buttons, in order.</summary>
    public IReadOnlyList<ButtonBase> Tools => _tools;

    /// <summary>Applies a toolbar button to the selection, as a typed replacement so Undo takes it back.</summary>
    public void Format(MarkdownAction action)
    {
        if (IsReadOnly)
        {
            return;
        }

        var change = MarkdownFormat.Apply(action, Editor.Text, Editor.SelectionStart, Editor.SelectionLength);
        Editor.BeginChange();
        Editor.Select(change.Start, change.Length);
        Editor.SelectedText = change.Replacement;
        Editor.EndChange();
        Editor.Select(change.SelectionStart, change.SelectionLength);
        Editor.Focus();
    }

    /// <summary>Ctrl+B, Ctrl+I and Ctrl+K; whether the key was one of them.</summary>
    public bool HandleShortcut(Key key, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.Control || IsReadOnly)
        {
            return false;
        }

        MarkdownAction? action = key switch
        {
            Key.B => MarkdownAction.Bold,
            Key.I => MarkdownAction.Italic,
            Key.K => MarkdownAction.Link,
            _ => null,
        };

        if (action is { } chosen)
        {
            Format(chosen);
        }

        return action is not null;
    }

    private static void OnTextChanged(DependencyObject box, DependencyPropertyChangedEventArgs e)
    {
        if (box is MarkdownBox { IsPreviewing: true } markdown)
        {
            markdown._viewer.Document = MarkdownRenderer.Render(markdown.Text);
        }
    }

    private ButtonBase Tool(MarkdownAction action, string glyph, string toolTip, bool text = false, FontWeight? weight = null,
        FontStyle? style = null)
    {
        var button = new Button { Content = glyph, ToolTip = toolTip, Tag = toolTip, Focusable = false };
        button.SetResourceReference(StyleProperty, "ToolButton");

        if (text)
        {
            button.FontFamily = new FontFamily("Segoe UI");
            button.FontWeight = weight ?? FontWeights.SemiBold;
            button.FontStyle = style ?? FontStyles.Normal;
        }

        AutomationProperties.SetName(button, toolTip);
        button.Click += (_, _) => Format(action);
        _tools.Add(button);

        return button;
    }

    private static Button Tab(string label, string toolTip, Action choose)
    {
        var button = new Button { Content = label, ToolTip = toolTip, Padding = new Thickness(6, 1, 6, 1) };
        button.SetResourceReference(StyleProperty, "LinkButton");
        button.Click += (_, _) => choose();

        return button;
    }

    private void ShowMode()
    {
        if (_isPreviewing)
        {
            _viewer.Document = MarkdownRenderer.Render(Text);
        }

        Editor.Visibility = _isPreviewing ? Visibility.Collapsed : Visibility.Visible;
        _viewer.Visibility = _isPreviewing ? Visibility.Visible : Visibility.Collapsed;
        _write.FontWeight = _isPreviewing ? FontWeights.Normal : FontWeights.SemiBold;
        _preview.FontWeight = _isPreviewing ? FontWeights.SemiBold : FontWeights.Normal;
        ApplyReadOnly();
    }

    private void ApplyReadOnly()
    {
        Editor.IsReadOnly = IsReadOnly;

        // A disabled button says why (1.1e AC 6a).
        foreach (var tool in _tools)
        {
            tool.IsEnabled = !IsReadOnly && !_isPreviewing;
            tool.ToolTip = IsReadOnly ? ReadOnlyReason
                : _isPreviewing ? "Back to Write to change the text"
                : tool.Tag;
        }
    }
}
