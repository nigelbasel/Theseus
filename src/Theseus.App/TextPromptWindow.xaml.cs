using System.Windows;
using System.Windows.Controls;

namespace Theseus.App;

/// <summary>Asks for one line of text, such as an objective's new title (1.1c AC 8). OK stays off while it is empty.</summary>
public partial class TextPromptWindow : Window
{
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

    /// <summary>The text given, once OK was pressed.</summary>
    public string Text => Answer.Text.Trim();

    private void OnChanged(object sender, TextChangedEventArgs e) => OkButton.IsEnabled = Answer.Text.Trim().Length > 0;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
