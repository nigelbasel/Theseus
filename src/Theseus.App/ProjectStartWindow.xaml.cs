using System.Windows;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>A project's Start confirmation (1.1d AC 43): the ticked cards start when Start is pressed.</summary>
public partial class ProjectStartWindow : Window
{
    /// <summary>Creates the window over the confirmation.</summary>
    public ProjectStartWindow(ProjectStartViewModel question)
    {
        InitializeComponent();
        DataContext = question;
        Loaded += (_, _) => StartButton.Focus();
    }

    private void OnStart(object sender, RoutedEventArgs e) => DialogResult = true;
}
