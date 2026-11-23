using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A checklist confirmation: the rows ticked are what the press acts on.</summary>
public partial class ChecklistWindow : Window
{
    /// <summary>Creates the dialog over its model.</summary>
    public ChecklistWindow(ChecklistViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;
}
