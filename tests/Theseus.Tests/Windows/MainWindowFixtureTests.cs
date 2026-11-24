using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Theseus.Tests.Windows;

/// <summary>The MainWindow fixture itself (baseline WB6): it builds the window and shows the board with fake cards.</summary>
[TestFixture]
[NonParallelizable]
public class MainWindowFixtureTests
{
    [Test]
    public void TheFixture_BuildsTheWindow_AndShowsTheBoardWithFakeCards() =>
        MainWindowFixture.Run(
            (window, _) =>
            {
                FrameworkElement board = (FrameworkElement)window.FindName("BoardSurface");
                List<string> titles = [.. Descendants<TextBlock>(board).Where(text => text.Name == "CardTitle").Select(text => text.Text)];

                Assert.That(board.Visibility, Is.EqualTo(Visibility.Visible));
                Assert.That(titles, Is.SupersetOf(new[] { "alpha", "beta" }));
            },
            board => board.With("alpha").With("beta", column: "In progress"));

    [Test]
    public void TheToolbarsOverflow_AndTheSearchHistoryButton_AreNamed() =>
        MainWindowFixture.Run((window, _) =>
        {
            // Both come from theme templates with no name (E9, G167).
            ToolBar toolBar = (ToolBar)window.FindName("MainToolBar");
            toolBar.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Control search = (Control)window.FindName("SearchBox");

            List<string> names =
            [
                .. Descendants<System.Windows.Controls.Primitives.ToggleButton>(toolBar).Concat(Descendants<System.Windows.Controls.Primitives.ToggleButton>(search))
                    .Where(toggle => toggle.TemplatedParent is ToolBar or ComboBox)
                    .Select(System.Windows.Automation.AutomationProperties.GetName),
            ];

            Assert.That(names, Is.EquivalentTo(new[] { "More tools", "Recent searches" }));
        });

    internal static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T inner in Descendants<T>(child))
            {
                yield return inner;
            }
        }
    }
}
