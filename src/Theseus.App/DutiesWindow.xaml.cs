using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>The duties checklist: every duty of the board, a project or a card, and the actions on what is ticked.</summary>
public partial class DutiesWindow : Window
{
    /// <summary>Creates the dialog over its model.</summary>
    public DutiesWindow(DutiesViewModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        InitializeComponent();
        DataContext = model;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
