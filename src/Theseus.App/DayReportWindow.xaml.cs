using System.Windows;

namespace Theseus.App;

/// <summary>The day's work, as written to its file. A window of its own, not a dialog.</summary>
public partial class DayReportWindow : Window
{
    /// <summary>Creates the window.</summary>
    public DayReportWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Height = Math.Min(Height, SystemParameters.WorkArea.Height - 80);
    }
}
