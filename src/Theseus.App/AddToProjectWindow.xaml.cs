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
    /// <summary>Creates the window, with a project chosen when it is asked from that project's view (1.1e AC 6).</summary>
    public AddToProjectWindow(IReadOnlyList<ProjectOption> projects, string slug, string? chosen = null)
    {
        InitializeComponent();
        ProjectBox.ItemsSource = projects;
        SlugBox.Text = slug;
        ProjectBox.SelectedIndex = ProjectOption.IndexToChoose(projects, chosen);
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

    // A disabled Add says why on hover (W13): the first thing still missing.
    private void OnChanged(object sender, RoutedEventArgs e)
    {
        string? missing = ProjectBox.SelectedItem is null ? "Choose a project first."
            : ObjectiveBox.SelectedItem is null ? "Choose one of its objectives first."
            : !ProjectMembership.IsSlug(SlugBox.Text.Trim()) ? "Name the records folder in lower-case letters, digits and hyphens first."
            : null;
        AddButton.IsEnabled = missing is null;
        AddButton.ToolTip = missing;
    }

    private void OnAdd(object sender, RoutedEventArgs e) => DialogResult = true;
}
