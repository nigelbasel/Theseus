using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// Project properties (1.1d AC 40; 1.1e AC 12a; options-made-clear): OK writes a changed title and keeps the colour, lead,
/// branch rules, holdout folder and private repository chosen; Cancel keeps none of them (baseline W6). A refusal keeps the
/// dialog open with the reason under the field it is about (A19).
/// </summary>
public partial class ProjectPropertiesWindow : Window
{
    /// <summary>Creates the window over a project's properties, at the tab it was asked for.</summary>
    public ProjectPropertiesWindow(ProjectPropertiesViewModel properties)
    {
        InitializeComponent();
        DataContext = properties;

        if (properties.OpenAt == ProjectPropertiesViewModel.HoldoutsTab)
        {
            Tabs.SelectedItem = HoldoutsTab;
        }

        // The first control of the tab it opens at (baseline B3, W43).
        Loaded += (_, _) =>
        {
            if (Tabs.SelectedItem == GeneralTab)
            {
                ColourBox.Focus();
            }
            else if (Tabs.SelectedItem == HoldoutsTab)
            {
                (ResetHoldoutButton.IsEnabled ? ResetHoldoutButton : ChangeHoldoutButton).Focus();
            }
        };
    }

    private ProjectPropertiesViewModel Properties => (ProjectPropertiesViewModel)DataContext;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Properties.Apply())
        {
            DialogResult = true;

            return;
        }

        // The refusal is shown on its own tab, so OK never seems to do nothing.
        if (Properties.HasHoldoutError)
        {
            Tabs.SelectedItem = HoldoutsTab;
        }
        else if (Properties.HasError)
        {
            Tabs.SelectedItem = GeneralTab;
        }
    }

    // Change beside the lead: a menu of the project's workstreams, the lead ticked. With none, the button is off and says why.
    private void OnChooseLead(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = LeadChange };

        foreach (var (key, shown) in Properties.Leads)
        {
            var item = new MenuItem
            {
                Header = shown,
                IsCheckable = true,
                IsChecked = string.Equals(key, Properties.Lead, StringComparison.OrdinalIgnoreCase),
            };
            item.Click += (_, _) => Properties.Lead = key;
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.IsOpen = true;
        }
    }

    private void OnChooseHoldout(object sender, RoutedEventArgs e)
    {
        var current = Properties.HoldoutValue;
        var dialog = new OpenFolderDialog
        {
            Title = $"Choose the holdout folder of {Properties.ShownTitle}",
            InitialDirectory = Directory.Exists(current) ? current : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        // A refusal is shown under the holdout row by the view model (A19).
        if (dialog.ShowDialog(this) == true)
        {
            Properties.ChooseHoldout(dialog.FolderName);
        }
    }

    private void OnResetHoldout(object sender, RoutedEventArgs e) => Properties.ResetHoldout();

    private void OnKeepInPrivateRepository(object sender, RoutedEventArgs e)
    {
        if (Properties.Holdout is not { IsDefault: true })
        {
            return;
        }

        // Held until OK, so Cancel adds no remote (A2); a refusal at OK shows under the holdout row (A19).
        TextPromptWindow ask = new("Use a private repository",
            $"The address of a private repository for {Properties.ShownTitle}'s holdout scenarios", string.Empty)
        {
            Owner = this,
            ActionLabel = "_Add",
            Validator = ProjectPropertiesViewModel.WhyNotRemote,
        };

        if (ask.Ask() is { } address)
        {
            Properties.KeepInPrivateRepository(address);
        }
    }
}
