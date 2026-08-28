using System.Windows;

namespace Theseus.App;

/// <summary>
/// What the buttons do and how they fail, as a window of its own. A tooltip can say what a control
/// is; it cannot explain a process that runs through a terminal, a launcher and a hook inside
/// another process, which is exactly the part nobody can see.
/// </summary>
public partial class HelpWindow : Window
{
    /// <summary>Creates the window.</summary>
    public HelpWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => FitToWorkArea();
    }

    private void FitToWorkArea()
    {
        MaxHeight = SystemParameters.WorkArea.Height - 80;
        Height = Math.Min(Height, MaxHeight);
    }
}
