using System.Windows;

namespace Theseus.App;

/// <summary>
/// The window arranger, as a window of its own and not a dialog. It stays open while windows move
/// around underneath it, which is the point: an arrangement is judged by looking at the result.
/// </summary>
public partial class ArrangeWindow : Window
{
    /// <summary>Creates the window.</summary>
    public ArrangeWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Height = Math.Min(Height, SystemParameters.WorkArea.Height - 80);
    }
}
