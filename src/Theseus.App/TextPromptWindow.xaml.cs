using System.Windows;
using System.Windows.Controls;

namespace Theseus.App;

/// <summary>
/// Asks for a line of text, such as an objective's new title (1.1c AC 8), or a short list, one item per line. OK stays off
/// while it is empty unless empty is allowed, as for an address that may be cleared.
/// </summary>
public partial class TextPromptWindow : Window
{
    private bool _allowsEmpty;

    /// <summary>Creates the window.</summary>
    public TextPromptWindow(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        Label.Text = label;
        Answer.Text = initial;
        OkButton.IsEnabled = initial.Trim().Length > 0;
        Loaded += (_, _) =>
        {
            Answer.Focus();
            Answer.SelectAll();
        };
    }

    /// <summary>Whether OK may give an empty answer.</summary>
    public bool AllowsEmpty
    {
        get => _allowsEmpty;
        init
        {
            _allowsEmpty = value;
            OkButton.IsEnabled = value || Answer.Text.Trim().Length > 0;
        }
    }

    /// <summary>Whether the answer is a list, one item per line.</summary>
    public bool IsList
    {
        get => Answer.AcceptsReturn;
        init
        {
            Answer.AcceptsReturn = value;
            Answer.MinHeight = value ? 140 : 0;
            Answer.VerticalScrollBarVisibility = value ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        }
    }

    /// <summary>The text given, once OK was pressed.</summary>
    public string Text => Answer.Text.Trim();

    private void OnChanged(object sender, TextChangedEventArgs e) => OkButton.IsEnabled = _allowsEmpty || Answer.Text.Trim().Length > 0;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
