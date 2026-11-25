using System.Windows;
using System.Windows.Controls.Primitives;
using Theseus.App;
using Theseus.App.Controls;

namespace Theseus.Tests.Windows;

/// <summary>
/// The side panels and the drawer are resizable by their inner edge, within a minimum and a maximum (baseline WB7;
/// finding D13; W39; checklist G115). The drag is raised as the thumb's own event, so no pointer is moved.
/// </summary>
[TestFixture]
[NonParallelizable]
public class PaneResizeTests
{
    [TestCase("SessionsPane", 40, 320)]
    [TestCase("ChainsPane", 40, 300)]
    [TestCase("LocksPane", 40, 300)]
    [TestCase("DrawerPane", 40, 440)]
    public void DraggingItsEdge_ResizesThePane(string pane, double drag, double expected) =>
        MainWindowFixture.Run((window, _) =>
        {
            PaneResizeThumb edge = EdgeOf(window, pane);

            edge.RaiseEvent(new DragDeltaEventArgs(drag, 0));

            Assert.That(((FrameworkElement)window.FindName(pane)).Width, Is.EqualTo(expected));
        });

    [TestCase("SessionsPane")]
    [TestCase("ChainsPane")]
    [TestCase("DrawerPane")]
    public void ThePane_StaysWithinItsMinimumAndMaximum(string pane) =>
        MainWindowFixture.Run((window, _) =>
        {
            FrameworkElement target = (FrameworkElement)window.FindName(pane);
            PaneResizeThumb edge = EdgeOf(window, pane);

            edge.RaiseEvent(new DragDeltaEventArgs(-5000, 0));
            double one = target.Width;
            edge.RaiseEvent(new DragDeltaEventArgs(5000, 0));
            double other = target.Width;

            Assert.That(new[] { one, other }, Is.EquivalentTo(new[] { target.MinWidth, target.MaxWidth }));
            Assert.That(target.MinWidth, Is.GreaterThan(0));
            Assert.That(target.MaxWidth, Is.LessThan(double.PositiveInfinity));
        });

    private static PaneResizeThumb EdgeOf(MainWindow window, string pane) =>
        MainWindowFixtureTests.Descendants<PaneResizeThumb>((DependencyObject)window.FindName(pane)).Single();
}
