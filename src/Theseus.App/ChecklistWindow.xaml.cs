using System.Windows;
using Theseus.App.Controls;
using Theseus.App.Text;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A checklist confirmation: the rows ticked are what the press acts on.</summary>
public partial class ChecklistWindow : Window
{
    /// <summary>Creates the dialog over its model; the keyboard starts on the first row (W43).</summary>
    public ChecklistWindow(ChecklistViewModel model)
    {
        InitializeComponent();
        DataContext = model;

        // The label's first letter is its access key (B9); a bound or formatted label would draw the underscore instead.
        ConfirmButton.Content = "_" + AccessKeyText.Escape(model.ConfirmLabel);
        Loaded += (_, _) => FirstRow.Focus(Rows);
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    // A disabled commit says why on hover (W13); an enabled one needs no tooltip.
    private void OnConfirmEnabledChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        ConfirmButton.ToolTip = ConfirmButton.IsEnabled ? null : "Tick at least one row first.";
}
