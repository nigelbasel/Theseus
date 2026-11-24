using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Theseus.App.ViewModels;

namespace Theseus.App;

/// <summary>Every line of a project's Waits for you, each with its button (project-view-scale, 2026-11-24).</summary>
public partial class WaitsWindow : Window
{
    /// <summary>Creates the dialog over its model.</summary>
    public WaitsWindow(WaitsListViewModel model)
    {
        InitializeComponent();
        DataContext = model ?? throw new ArgumentNullException(nameof(model));
        Loaded += (_, _) => FocusFirst();
    }

    // The keyboard starts on the first row that has a button (W43); a list of things outside Theseus starts on Close.
    private void FocusFirst()
    {
        Rows.UpdateLayout();
        var first = Enumerable.Range(0, Rows.Items.Count)
            .Select(index => Rows.ItemContainerGenerator.ContainerFromIndex(index) as DependencyObject)
            .Select(row => row is null ? null : FindButton(row))
            .FirstOrDefault(button => button is { IsVisible: true });

        ((UIElement?)first ?? CloseButton).Focus();
    }

    private static Button? FindButton(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);

            if ((child as Button ?? FindButton(child)) is { } button)
            {
                return button;
            }
        }

        return null;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    // The click is raised before the button's command runs, so the command still runs after the dialog closes: what it
    // does, such as bringing a terminal forward, then comes to the front.
    private void OnRowButton(object sender, RoutedEventArgs e) => Close();
}
