using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Opens a window off-screen with the app's own resources and the Fluent theme it runs under, so its styles resolve as they
/// do in Theseus, and its Loaded handlers run. The tests run without an Application, so App.xaml's resources are merged
/// into the window itself.
/// </summary>
public static class DialogHost
{
    /// <summary>Shows <paramref name="window"/> far off-screen, lets it load, and returns it; the caller closes it.</summary>
    public static Window Open(Window window)
    {
        foreach (string source in new[] { "Fluent.xaml", "Fluent.Light.xaml" })
        {
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{source}"),
            });
        }

        window.Resources.MergedDictionaries.Add(AppResources());
        window.Owner = null;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -30000;
        window.Top = 0;
        window.ShowInTaskbar = false;
        window.Show();
        Pump();

        return window;
    }

    /// <summary>Runs the dispatcher until everything queued down to idle has run, Loaded handlers included.</summary>
    public static void Pump()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    /// <summary>Every visual descendant of <paramref name="parent"/>, in tree order.</summary>
    public static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            yield return child;

            foreach (DependencyObject inner in Descendants(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>App.xaml's resources, its theme dictionaries included, read from the source tree.</summary>
    public static ResourceDictionary AppResources()
    {
        string text = File.ReadAllText(LooseXaml.SourceFile("App.xaml"));
        int open = text.IndexOf("<ResourceDictionary>", StringComparison.Ordinal);
        int close = text.LastIndexOf("</ResourceDictionary>", StringComparison.Ordinal) + "</ResourceDictionary>".Length;
        string body = text[open..close]
            .Replace("<ResourceDictionary>", "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" "
                + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\" "
                + "xmlns:converters=\"clr-namespace:Theseus.App.Converters;assembly=Theseus\">", StringComparison.Ordinal)
            .Replace("Source=\"Themes/", "Source=\"pack://application:,,,/Theseus;component/Themes/", StringComparison.Ordinal);

        return (ResourceDictionary)XamlReader.Parse(body);
    }
}
