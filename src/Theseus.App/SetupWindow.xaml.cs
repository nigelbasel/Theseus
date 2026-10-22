using System.Windows;

namespace Theseus.App;

/// <summary>
/// A card's setup, in a dialog that moves and closes like any other. It grows with what it holds and
/// stops short of the screen's height, after which only its middle scrolls.
/// </summary>
public partial class SetupWindow : Window
{
    /// <summary>Creates the window.</summary>
    public SetupWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => MaxHeight = SystemParameters.WorkArea.Height - 80;
    }
}
