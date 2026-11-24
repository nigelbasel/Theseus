using System.Windows;
using System.Windows.Controls;
using Theseus.App.Controls;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>The waits checklist: every wait of a project, ticked or not, and the actions on what is ticked.</summary>
public partial class WaitsWindow : Window
{
    /// <summary>Creates the dialog over its model; the keyboard starts on the first row (W43).</summary>
    public WaitsWindow(WaitsChecklistViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContext = model;
        model.Finished += (_, _) => Close();
        Loaded += (_, _) => FirstRow.Focus(Rows);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // The click is raised before the button's command runs, so the command still runs after the dialog closes, and what it
    // brings forward comes to the front.
    private void OnRowButton(object sender, RoutedEventArgs e) => Close();
}
