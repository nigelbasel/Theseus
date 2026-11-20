using System.Windows;
using System.Windows.Controls;
using Theseus.Core.Projects;

namespace Theseus.App;

/// <summary>Asks the name of a new project (1.1b AC 7); Create stays off until the name has a slug's shape.</summary>
public partial class NewProjectWindow : Window
{
    /// <summary>Creates the window.</summary>
    public NewProjectWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
    }

    /// <summary>The name given, once Create was pressed.</summary>
    public string ProjectName => NameBox.Text.Trim();

    private void OnNameChanged(object sender, TextChangedEventArgs e) => CreateButton.IsEnabled = ProjectMembership.IsSlug(ProjectName);

    private void OnCreate(object sender, RoutedEventArgs e) => DialogResult = true;
}
