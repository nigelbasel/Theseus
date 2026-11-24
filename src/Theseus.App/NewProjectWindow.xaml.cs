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

    // A disabled Create says why on hover (W13), and the hint under a name that cannot be used turns critical (A15). An
    // empty name is not yet wrong, so its hint stays as it was.
    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        CreateButton.IsEnabled = ProjectMembership.IsSlug(name);
        CreateButton.ToolTip = CreateButton.IsEnabled ? null
            : name.Length == 0 ? "Give the project a name first."
            : "Use only lower-case letters, digits and hyphens in the name.";
        NameHint.SetResourceReference(TextBlock.ForegroundProperty,
            CreateButton.IsEnabled || name.Length == 0 ? "TextFillColorSecondaryBrush" : "CriticalTextBrush");
    }

    private void OnCreate(object sender, RoutedEventArgs e) => DialogResult = true;
}
