using System.Windows;
using System.Windows.Controls;
using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.App;

/// <summary>
/// Add to project… (1.1c AC 7): a readable project, one of its objectives, and the slug, which starts as the card's
/// name. Add stays off until all three are chosen and the slug has a slug's shape.
/// </summary>
public partial class AddToProjectWindow : Window
{
    /// <summary>Creates the window.</summary>
    public AddToProjectWindow(IReadOnlyList<ProjectOption> projects, string slug)
    {
        InitializeComponent();
        ProjectBox.ItemsSource = projects;
        SlugBox.Text = slug;
        ProjectBox.SelectedIndex = projects.Count == 1 ? 0 : -1;
        Loaded += (_, _) => ProjectBox.Focus();
    }

    /// <summary>What was chosen, once Add was pressed.</summary>
    public AddToProjectChoice Choice =>
        new(((ProjectOption)ProjectBox.SelectedItem).Name, ((string)ObjectiveBox.SelectedItem).Split(' ')[0], SlugBox.Text.Trim());

    private void OnProjectChanged(object sender, SelectionChangedEventArgs e)
    {
        ObjectiveBox.ItemsSource = ProjectBox.SelectedItem is ProjectOption project
            ? project.Objectives.Select(objective => $"{objective.Number} {objective.Title}").ToList()
            : [];
        OnChanged(sender, e);
    }

    private void OnChanged(object sender, RoutedEventArgs e) =>
        AddButton.IsEnabled = ProjectBox.SelectedItem is not null && ObjectiveBox.SelectedItem is not null
                              && ProjectMembership.IsSlug(SlugBox.Text.Trim());

    private void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;
}
