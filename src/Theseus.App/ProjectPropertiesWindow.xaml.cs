using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// Project properties… (1.1d AC 40; 1.1e AC 12a): OK writes a changed title and keeps the colour chosen; a title the file
/// refuses keeps the dialog open with the reason.
/// </summary>
public partial class ProjectPropertiesWindow : Window
{
    /// <summary>Creates the window over a project's properties.</summary>
    public ProjectPropertiesWindow(ProjectPropertiesViewModel properties)
    {
        InitializeComponent();
        DataContext = properties;
        Loaded += (_, _) => ColourBox.Focus();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (((ProjectPropertiesViewModel)DataContext).Apply())
        {
            DialogResult = true;
        }
    }
}
