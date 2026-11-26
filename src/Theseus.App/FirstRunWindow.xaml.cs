using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Win32;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>
/// The first-run setup's window over a <see cref="FirstRunViewModel"/>: it closes when the setup does, and closing it by the
/// title bar or Escape is Skip setup. The folder pickers are here because a picker is a window.
/// </summary>
public partial class FirstRunWindow : Window
{
    private bool _closing;

    /// <summary>Creates the window.</summary>
    public FirstRunWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, changed) =>
        {
            if (changed.OldValue is FirstRunViewModel old)
            {
                old.PropertyChanged -= OnSetupChanged;
            }

            if (changed.NewValue is FirstRunViewModel setup)
            {
                setup.PropertyChanged += OnSetupChanged;
            }
        };
        Loaded += (_, _) => ForwardButton.Focus();
    }

    private FirstRunViewModel? Setup => DataContext as FirstRunViewModel;

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        // Already closing: the setup's turning shut must not ask the window to close a second time.
        _closing = true;
        Setup?.Closed();
        base.OnClosing(e);
    }

    private void OnSetupChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FirstRunViewModel.IsOpen) && Setup is { IsOpen: false } && IsLoaded && !_closing)
        {
            Close();
        }
    }

    private void OnChooseProjectsFolder(object sender, RoutedEventArgs e)
    {
        if (Setup is { } setup && Choose("Choose the projects folder", setup.ProjectsFolder) is { } folder)
        {
            setup.ProjectsFolder = folder;
        }
    }

    private void OnChooseWorktreesFolder(object sender, RoutedEventArgs e)
    {
        if (Setup is { } setup && Choose("Choose the worktrees folder", setup.WorktreesFolder) is { } folder)
        {
            setup.WorktreesFolder = folder;
        }
    }

    private void OnChooseClonesFolder(object sender, RoutedEventArgs e)
    {
        if (Setup is { } setup && Choose("Choose the clones folder", setup.ClonesFolder) is { } folder)
        {
            setup.ClonesFolder = folder;
        }
    }

    // Nothing is chosen for the developer: the step only suggests (Nigel, 2026-11-20, question 1), as Options does.
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
