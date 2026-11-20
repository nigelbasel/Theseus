using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// The settings editor, as a window of its own rather than a panel inside the board. A dialog drawn
/// inside the application window is bounded by it, and the settings are taller than a small window:
/// its footer went off the bottom edge where nothing could reach it.
/// </summary>
public partial class SettingsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public SettingsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => FitToWorkArea();
    }

    private void FitToWorkArea() =>
        MaxHeight = SystemParameters.WorkArea.Height - 80;

    private void OnChooseProjectsFolder(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel settings && Choose("Choose the projects folder", settings.ProjectsFolder) is { } folder)
        {
            settings.ProjectsFolder = folder;
        }
    }

    private void OnChooseWorktreesFolder(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel settings && Choose("Choose the worktrees folder", settings.WorktreesFolder) is { } folder)
        {
            settings.WorktreesFolder = folder;
        }
    }

    // Nothing is chosen for the developer: the panel only suggests (Nigel, 2026-11-20, question 1).
    private string? Choose(string title, string current)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = !string.IsNullOrWhiteSpace(current) && Directory.Exists(current)
                ? current
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };

        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
