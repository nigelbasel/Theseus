using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Theseus.App.Views;

/// <summary>
/// The project view's tab strip (project-view-as-tabs AC 1): one row that scrolls with ‹ › and the wheel, keeping the
/// open tab in view. An arrow key in the strip opens the tab it reaches; the tabs are rebuilt then, and the new open tab
/// takes the focus, so the next arrow works.
/// </summary>
public partial class ProjectTabStrip : UserControl
{
    private const double Step = 120;
    private bool _hadFocus;

    /// <summary>Creates the strip.</summary>
    public ProjectTabStrip()
    {
        InitializeComponent();
        Tabs.ItemContainerGenerator.StatusChanged += OnTabsGenerated;
        Loaded += (_, _) => UpdateScrollButtons();
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e) => _hadFocus |= Tabs.IsKeyboardFocusWithin;

    private void OnTabsGenerated(object? sender, EventArgs e)
    {
        if (Tabs.ItemContainerGenerator.Status != System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        var refocus = _hadFocus;
        _hadFocus = false;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            if (Tabs.ItemContainerGenerator.ContainerFromItem(Tabs.SelectedItem) is ListBoxItem open)
            {
                open.BringIntoView();

                if (refocus)
                {
                    open.Focus();
                }
            }

            UpdateScrollButtons();
        });
    }

    private void OnScrollLeft(object sender, RoutedEventArgs e) => TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset - Step);

    private void OnScrollRight(object sender, RoutedEventArgs e) => TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset + Step);

    // The wheel scrolls the row sideways: the strip has no vertical scroll.
    private void OnTabWheel(object sender, MouseWheelEventArgs e)
    {
        TabScroller.ScrollToHorizontalOffset(TabScroller.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void OnTabsScrolled(object sender, ScrollChangedEventArgs e) => UpdateScrollButtons();

    // ‹ and › show only while the tabs do not fit, and each is off at its end.
    private void UpdateScrollButtons()
    {
        var overflows = TabScroller.ScrollableWidth > 0;
        ScrollLeft.Visibility = ScrollRight.Visibility = overflows ? Visibility.Visible : Visibility.Collapsed;
        ScrollLeft.IsEnabled = TabScroller.HorizontalOffset > 0;
        ScrollRight.IsEnabled = TabScroller.HorizontalOffset < TabScroller.ScrollableWidth;
    }
}
