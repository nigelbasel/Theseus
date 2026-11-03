using System.Windows;

namespace Theseus.App;

/// <summary>
/// New workstream, in a dialog that moves, resizes and closes like any other. It grows with what it
/// holds and stops short of the screen's height, after which only its middle scrolls, so Create and
/// Cancel are always in view.
/// </summary>
public partial class NewWorkstreamWindow : Window
{
    /// <summary>Creates the window.</summary>
    public NewWorkstreamWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MaxHeight = SystemParameters.WorkArea.Height - 80;
    }
}
