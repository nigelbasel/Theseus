using System.Windows;
using System.Windows.Controls;
using Theseus.Core.Projects;

namespace Theseus.App;

/// <summary>
/// New project (1.1c AC 6), and Convert to project… with its name and title filled in from the card (AC 7). Create
/// stays off until the name has a slug's shape.
/// </summary>
public partial class NewProjectWindow : Window
{
    /// <summary>Creates the window, optionally filled in.</summary>
    public NewProjectWindow(string name = "", string title = "")
    {
        InitializeComponent();
        NameBox.Text = name;
        TitleBox.Text = title;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>What was given, once Create was pressed.</summary>
    public NewProjectDetails Details =>
        new(NameBox.Text.Trim(), TitleBox.Text.Trim(), ObjectiveBox.Text.Trim(), PrimaryBox.Text.Trim());

    private void OnNameChanged(object sender, TextChangedEventArgs e) => CreateButton.IsEnabled = ProjectMembership.IsSlug(NameBox.Text.Trim());

    private void OnCreate(object sender, RoutedEventArgs e) => DialogResult = true;
}
