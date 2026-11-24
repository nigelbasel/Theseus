using System.Windows;
using System.Windows.Controls;

namespace Theseus.App;

/// <summary>
/// Asks for a line of text, such as an objective's new title (1.1c AC 8), or a short list, one item per line. Its action
/// stays off while the answer is empty unless empty is allowed, as for an address that may be cleared, and says why on
/// hover (baseline W13, A15). A validator's refusal is shown under the box and keeps the prompt open (A19).
/// </summary>
public partial class TextPromptWindow : Window
{
    private const string EmptyReason = "Type an answer first.";

    private bool _allowsEmpty;

    /// <summary>Creates the window.</summary>
    public TextPromptWindow(string title, string label, string initial)
    {
        InitializeComponent();
        Title = title;
        Label.Text = label;
        Answer.Text = initial;
        UpdateAction();
        Loaded += (_, _) =>
        {
            Answer.Focus();
            Answer.SelectAll();
        };
    }

    /// <summary>The action button's label, with its access key, such as "_Add" (A-m2); OK when unset, with no key (B10).</summary>
    public string ActionLabel
    {
        get => (string)OkButton.Content;
        init => OkButton.Content = value;
    }

    /// <summary>Why an answer cannot be taken, or null when it can; asked at the action, before the prompt closes.</summary>
    public Func<string, string?>? Validator { get; init; }

    /// <summary>Whether the action was taken with an answer the validator accepted.</summary>
    public bool Accepted { get; private set; }

    /// <summary>Whether the action may give an empty answer.</summary>
    public bool AllowsEmpty
    {
        get => _allowsEmpty;
        init
        {
            _allowsEmpty = value;
            UpdateAction();
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

    /// <summary>The text given, once the action was taken.</summary>
    public string Text => Answer.Text.Trim();

    /// <summary>Shows it over its owner and gives the answer, or null when cancelled.</summary>
    public string? Ask()
    {
        ShowDialog();

        return Accepted ? Text : null;
    }

    private void OnChanged(object sender, TextChangedEventArgs e)
    {
        Refusal.Visibility = Visibility.Collapsed;
        UpdateAction();
    }

    private void UpdateAction()
    {
        OkButton.IsEnabled = _allowsEmpty || Answer.Text.Trim().Length > 0;
        OkButton.ToolTip = OkButton.IsEnabled ? null : EmptyReason;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Validator?.Invoke(Text) is { } why)
        {
            Refusal.Text = why;
            Refusal.Visibility = Visibility.Visible;
            Answer.Focus();

            return;
        }

        Accepted = true;
        QuestionWindow.Finish(this, true);
    }
}
