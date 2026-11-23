using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// Project properties… (1.1d AC 40; 1.1e AC 12a; options-made-clear): OK writes a changed title and keeps the colour, lead,
/// branch rules and holdout folder chosen; a title the file refuses keeps the dialog open with the reason.
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

        Loaded += (_, _) =>
        {
            if (Tabs.SelectedItem == GeneralTab)
            {
                ColourBox.Focus();
            }
        };
    }

    private ProjectPropertiesViewModel Properties => (ProjectPropertiesViewModel)DataContext;

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (Properties.Apply())
        {
            DialogResult = true;
        }
    }

    // Change… beside the lead: a menu of the project's workstreams, the lead ticked.
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

        if (menu.Items.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "The project has no workstreams yet", IsEnabled = false });
        }

        menu.IsOpen = true;
    }

    private void OnChooseHoldout(object sender, RoutedEventArgs e)
    {
        var current = Properties.HoldoutValue;
        var dialog = new OpenFolderDialog
        {
            Title = $"Choose the holdout folder of {Properties.ShownTitle}",
            InitialDirectory = Directory.Exists(current) ? current : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        if (dialog.ShowDialog(this) == true && Properties.ChooseHoldout(dialog.FolderName) is { } why)
        {
            MessageBox.Show(this, why, "Holdout folder", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnResetHoldout(object sender, RoutedEventArgs e) => Properties.ResetHoldout();

    private void OnKeepInPrivateRepository(object sender, RoutedEventArgs e)
    {
        if (Properties.Holdout is not { IsDefault: true } own)
        {
            return;
        }

        var ask = new TextPromptWindow("Keep these in a private repository",
            $"The address of a private repository for {Properties.ShownTitle}'s holdout scenarios", string.Empty)
        {
            Owner = this,
        };

        if (ask.ShowDialog() == true && own.AddRemote(ask.Text) is { } why)
        {
            MessageBox.Show(this, why, "Keep these in a private repository", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
