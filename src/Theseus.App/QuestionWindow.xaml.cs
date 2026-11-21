using System.Windows;

namespace Theseus.App;

/// <summary>
/// A yes-or-no question whose yes button names the action, such as Stop / Cancel before stopping a card of a running
/// project (1.1e AC 10). Cancel is the default, so Enter does nothing the owner did not choose.
/// </summary>
public partial class QuestionWindow : Window
{
    /// <summary>Creates the window.</summary>
    /// <param name="title">Its title.</param>
    /// <param name="question">What it asks.</param>
    /// <param name="action">The action button's label, such as "Stop".</param>
    public QuestionWindow(string title, string question, string action)
    {
        InitializeComponent();
        Title = title;
        Question.Text = question;
        ActionButton.Content = action;
    }

    private void OnAction(object sender, RoutedEventArgs e) => DialogResult = true;
}
