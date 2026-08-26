using System.Windows;

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
}
