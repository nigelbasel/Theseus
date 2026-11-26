using System.Windows;
using System.Windows.Controls;
using Theseus.Core.Projects;

namespace Theseus.App;

/// <summary>
/// New project (1.1c AC 6), and Convert to project… with its name and title filled in from the card (AC 7). Create
/// stays off until the name has a slug's shape. The name follows the title until it is changed by hand, and follows it
/// again once it is cleared.
/// </summary>
public partial class NewProjectWindow : Window
{
    private bool _nameFollowsTitle = true;
    private bool _primaryFollowsName = true;

    /// <summary>Creates the window, optionally filled in.</summary>
    public NewProjectWindow(string name = "", string title = "")
    {
        InitializeComponent();
        TitleBox.Text = title;
        NameBox.Text = name.Length == 0 ? ProjectMembership.SlugFrom(title) : name;
        PrimaryBox.Text = Suggested();
        Loaded += (_, _) =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        };
    }

    /// <summary>What was given, once Create was pressed.</summary>
    public NewProjectDetails Details =>
        new(NameBox.Text.Trim(), TitleBox.Text.Trim(), ObjectiveBox.Text.Trim(), PrimaryBox.Text.Trim())
        {
            GrantBranchRules = GrantBox.IsChecked == true,
        };

    private string Suggested() => NewProjectDefaults.SuggestedPrimary(Environment.UserName, NameBox.Text);

    private void OnPrimaryChanged(object sender, TextChangedEventArgs e)
    {
        string primary = PrimaryBox.Text.Trim();
        _primaryFollowsName = primary.Length == 0 || primary == Suggested();
    }

    // A disabled Create says why on hover (W13), and the hint under a name that cannot be used turns critical (A15). An
    // empty name is not yet wrong, so its hint stays as it was.
    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        _nameFollowsTitle = name.Length == 0 || name == ProjectMembership.SlugFrom(TitleBox.Text);
        if (_primaryFollowsName && PrimaryBox is not null)
        {
            PrimaryBox.Text = Suggested();
        }
        CreateButton.IsEnabled = ProjectMembership.IsSlug(name);
        CreateButton.ToolTip = CreateButton.IsEnabled ? null
            : name.Length == 0 ? "Give the project a title or a name first."
            : "Use only lower-case letters, digits and hyphens in the name.";
        NameHint.SetResourceReference(TextBlock.ForegroundProperty,
            CreateButton.IsEnabled || name.Length == 0 ? "TextFillColorSecondaryBrush" : "CriticalTextBrush");
    }

    private void OnTitleChanged(object sender, TextChangedEventArgs e)
    {
        if (_nameFollowsTitle)
        {
            NameBox.Text = ProjectMembership.SlugFrom(TitleBox.Text);
        }
    }

    private void OnCreate(object sender, RoutedEventArgs e) => DialogResult = true;
}
