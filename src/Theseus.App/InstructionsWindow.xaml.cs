using System.Windows;

namespace Theseus.App;

/// <summary>What a workstream's sessions read and can run. A window of its own, so it can sit beside an editor.</summary>
public partial class InstructionsWindow : Window
{
    /// <summary>Creates the window.</summary>
    public InstructionsWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Height = Math.Min(Height, SystemParameters.WorkArea.Height - 80);
    }
}
