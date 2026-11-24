using System.Windows;
using System.Windows.Input;

namespace Theseus.App;

/// <summary>
/// A question whose buttons name their actions, such as Stop / Cancel before stopping a card of a running project
/// (1.1e AC 10), or Allow and start / Refuse / Decide later; or, in its information form, a statement with one Close
/// (baseline W10, 2026-11-23: no MessageBox, whose buttons cannot be renamed and whose Yes/No cannot be closed with Esc).
/// Cancel is the default, so Enter does nothing the owner did not choose.
/// </summary>
public partial class QuestionWindow : Window
{
    /// <summary>Creates a question with one action.</summary>
    /// <param name="title">Its title, the question itself where it can be (G7).</param>
    /// <param name="question">What it says.</param>
    /// <param name="action">The action button's label, such as "_Stop", with its access key.</param>
    public QuestionWindow(string title, string question, string action)
        : this(title, question, action, null)
    {
    }

    /// <summary>Creates a question with one or two actions.</summary>
    /// <param name="title">Its title, the question itself where it can be (G7).</param>
    /// <param name="question">What it says.</param>
    /// <param name="action">The first action's label, with its access key; it answers true.</param>
    /// <param name="secondAction">The second action's label, which answers false, or null for none.</param>
    /// <param name="cancel">The cancel button's label, which answers null; it is the default and Esc.</param>
    public QuestionWindow(string title, string question, string action, string? secondAction, string cancel = "Cancel")
    {
        InitializeComponent();
        Title = title;
        Question.Text = question;
        ActionButton.Content = action;
        CancelButton.Content = cancel;
        // The default button has the focus when it opens, never the action (baseline B3, W43).
        FocusManager.SetFocusedElement(this, CancelButton);

        if (secondAction is null)
        {
            Footer.Children.Remove(SecondActionButton);
        }
        else
        {
            SecondActionButton.Content = secondAction;
        }
    }

    /// <summary>The information form (W10): no action, only Close, which is both the default and Esc.</summary>
    /// <param name="title">Its title, built by the caller, such as "Can't start Auth yet" (A5).</param>
    /// <param name="text">What it says.</param>
    public static QuestionWindow Information(string title, string text)
    {
        QuestionWindow window = new(title, text, string.Empty, null, "Close");
        window.Footer.Children.Remove(window.ActionButton);
        window.CancelButton.Margin = new Thickness(0);

        return window;
    }

    /// <summary>True for the first action, false for the second, null when cancelled or closed.</summary>
    public bool? Answer { get; private set; }

    /// <summary>Shows it over its owner and gives the answer.</summary>
    public bool? Ask()
    {
        ShowDialog();

        return Answer;
    }

    private void OnAction(object sender, RoutedEventArgs e) => Choose(true);

    private void OnSecondAction(object sender, RoutedEventArgs e) => Choose(false);

    private void Choose(bool answer)
    {
        Answer = answer;
        Finish(this, answer);
    }

    /// <summary>
    /// Closes a dialog whose action was taken: through DialogResult while it is shown, so ShowDialog gives the result to
    /// callers that still read it, and by Close otherwise, since DialogResult throws on a window never shown, as in tests.
    /// </summary>
    internal static void Finish(Window window, bool result)
    {
        if (window.IsVisible)
        {
            window.DialogResult = result;
        }
        else
        {
            window.Close();
        }
    }
}
