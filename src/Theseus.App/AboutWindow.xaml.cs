using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace Theseus.App;

/// <summary>Help, About Theseus: the version, where it is installed and keeps its data, and its links.</summary>
public partial class AboutWindow : Window
{
    /// <summary>Creates the window.</summary>
    public AboutWindow() => InitializeComponent();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnLink(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
