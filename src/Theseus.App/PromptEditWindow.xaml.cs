using System.Windows;

namespace Theseus.App;

/// <summary>Edits one of the prompts Options keeps; OK gives the text back, Cancel leaves it as it was.</summary>
public partial class PromptEditWindow : Window
{
    /// <summary>Creates the window over a prompt's text.</summary>
    public PromptEditWindow(string title, string explanation, string text)
    {
        InitializeComponent();
        Title = title;
        Explanation.Text = explanation;
        Editor.Text = text;
        Loaded += (_, _) => Editor.Editor.Focus();
    }

    /// <summary>The text, once OK was pressed.</summary>
    public string Text => Editor.Text ?? string.Empty;

    private void OnOk(object sender, RoutedEventArgs e) => DialogResult = true;
}
