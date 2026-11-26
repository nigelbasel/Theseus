using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.ViewModels;

namespace Theseus.Tests.Windows;

/// <summary>
/// The mouse wheel and a touchpad's two-finger scroll move the Chains panel wherever the pointer is over it
/// (Nigel, 2026-11-25: two-finger scrolling on his laptop did not scroll the panel). The tree first sat in the panel's
/// scroll viewer with a scroll viewer of its own that had nowhere to scroll and still marked every wheel event handled.
/// Since the tree was virtualised it scrolls itself, and what waits above it scrolls apart. A precision touchpad sends
/// its scroll as many small wheel steps, so a step smaller than a mouse notch has to move each of them too.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ChainsPanelWheelTests
{
    private const int Notch = 120;

    [Test]
    public void TheWheelOverATreeRow_ScrollsThePanel() =>
        MainWindowFixture.Run((window, shell) =>
        {
            var panel = OpenWithLongChains(window, shell);
            var row = MainWindowFixtureTests.Descendants<TreeViewItem>((DependencyObject)window.FindName("ChainsTree")).First();

            Turn(row, -Notch);
            panel.UpdateLayout();

            Assert.That(panel.VerticalOffset, Is.GreaterThan(0), "the panel did not move under the wheel over a tree row");
        });

    [Test]
    public void TheWheelOverWhatWaitsAboveTheTree_ScrollsThatArea() =>
        MainWindowFixture.Run((window, shell) =>
        {
            OpenWithLongChains(window, shell);
            var top = (ScrollViewer)window.FindName("ChainsTop");
            top.MaxHeight = 12;
            top.UpdateLayout();
            Assume.That(top.ScrollableHeight, Is.GreaterThan(0), "the area above the tree overflows");
            var heading = MainWindowFixtureTests.Descendants<TextBlock>(top).First(text => text.Text == "Chains");

            Turn(heading, -12);
            top.UpdateLayout();

            Assert.That(top.VerticalOffset, Is.GreaterThan(0));
        });

    [Test]
    public void ATouchpadsSmallSteps_ScrollThePanelByWhatTheyAddUpTo() =>
        MainWindowFixture.Run((window, shell) =>
        {
            var panel = OpenWithLongChains(window, shell);
            var row = MainWindowFixtureTests.Descendants<TreeViewItem>((DependencyObject)window.FindName("ChainsTree")).First();

            for (var step = 0; step < 10; step++)
            {
                Turn(row, -12);
            }

            panel.UpdateLayout();
            var afterSmallSteps = panel.VerticalOffset;

            panel.ScrollToTop();
            panel.UpdateLayout();
            Turn(row, -Notch);
            panel.UpdateLayout();

            Assert.That(afterSmallSteps, Is.GreaterThan(0), "small steps did not move the panel");
            Assert.That(afterSmallSteps, Is.EqualTo(panel.VerticalOffset).Within(0.5),
                "ten steps of a tenth of a notch moved the panel by a different amount from one notch");
        });

    [Test]
    public void TheWheelBackUp_ScrollsThePanelBackUp() =>
        MainWindowFixture.Run((window, shell) =>
        {
            var panel = OpenWithLongChains(window, shell);
            var row = MainWindowFixtureTests.Descendants<TreeViewItem>((DependencyObject)window.FindName("ChainsTree")).First();

            Turn(row, -Notch);
            Turn(row, -Notch);
            panel.UpdateLayout();
            var down = panel.VerticalOffset;
            Turn(row, Notch);
            panel.UpdateLayout();

            Assert.That(panel.VerticalOffset, Is.LessThan(down));
        });

    // The panel, shown with more chains than fit, and its scroll viewer, laid out at the window's default size.
    private static ScrollViewer OpenWithLongChains(MainWindow window, FakeShell shell)
    {
        for (var chain = 0; chain < 40; chain++)
        {
            var root = Node($"Chain {chain}");
            root.Children.Add(Node($"Chain {chain} follow-on"));
            shell.Chains.Roots.Add(root);
        }

        var pane = (FrameworkElement)window.FindName("ChainsPane");
        pane.Visibility = Visibility.Visible;

        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(1400, 760));
        content.Arrange(new Rect(0, 0, 1400, 760));
        content.UpdateLayout();

        var panel = MainWindowFixtureTests.Descendants<ScrollViewer>((DependencyObject)window.FindName("ChainsTree")).First();
        Assert.That(panel.ScrollableHeight, Is.GreaterThan(Notch * 2), "the chains do not overflow the tree, so nothing can scroll");

        return panel;
    }

    // One wheel step as the input system raises it: the tunnelling preview first, then, if nothing handled that, the
    // bubbling event, both from the element under the pointer.
    private static void Turn(UIElement under, int delta)
    {
        var preview = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = UIElement.PreviewMouseWheelEvent };
        under.RaiseEvent(preview);

        if (preview.Handled)
        {
            return;
        }

        under.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, delta) { RoutedEvent = UIElement.MouseWheelEvent });
    }

    private static ChainNodeViewModel Node(string title) =>
        new(title.Replace(' ', '-'), title, "#C0392B", "Ready", IsReady: true, string.Empty, string.Empty, new RelayCommand(_ => { }));
}
