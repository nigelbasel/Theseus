using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Theseus.App.ViewModels;
using Theseus.Tests.Accessibility;

namespace Theseus.Tests.Windows;

/// <summary>
/// The Chains panel's search box and Filter menu as the window builds them (chains-panel-at-scale criteria 1, 6, 8, 20 and
/// 21): named for a screen reader, the menu's three filters and Clear filters, and Esc clearing the box.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ChainsPanelSearchTests
{
    [Test]
    public void TheSearchBox_AndTheFilterButton_AreNamed() =>
        MainWindowFixture.Run((window, _) =>
        {
            var box = (TextBox)window.FindName("ChainsSearchBox");
            var filter = (Button)window.FindName("ChainsFilterButton");

            Assert.That(AutomationName(box), Is.EqualTo("Find in chains"));
            Assert.That(box.GetValue(System.Windows.Automation.AutomationProperties.AcceleratorKeyProperty), Is.EqualTo("Ctrl+F"));
            Assert.That(AutomationName(filter), Is.EqualTo("Filter"));
        });

    [Test]
    public void TheFilterMenu_HoldsTheThreeFilters_AsCheckableItems_AndClearFilters() =>
        MainWindowFixture.Run((window, _) =>
        {
            var menu = ((Button)window.FindName("ChainsFilterButton")).ContextMenu!;
            var items = menu.Items.OfType<MenuItem>().ToList();

            Assert.That(items.Take(3).Select(item => item.IsCheckable), Is.All.True);
            Assert.That(items.Take(3).Select(item => System.Windows.Data.BindingOperations.GetBinding(item, MenuItem.IsCheckedProperty)!.Path.Path),
                Is.EqualTo(new[] { "Chains.ActiveOnly", "Chains.WaitingOnMe", "Chains.HideCompleted" }));
            Assert.That(items.Last().Header, Is.EqualTo("Clear filters"));
        });

    [Test]
    public void Tab_ReachesTheSearchBox_BeforeTheFilterButton() =>
        MainWindowFixture.Run((window, _) =>
        {
            var finder = (DependencyObject)window.FindName("ChainsFinder");
            var inOrder = MainWindowFixtureTests.Descendants<Control>(finder).Where(control => control.IsTabStop && control.Focusable).ToList();

            Assert.That(inOrder.IndexOf((TextBox)window.FindName("ChainsSearchBox")),
                Is.LessThan(inOrder.IndexOf((Button)window.FindName("ChainsFilterButton"))));
        });

    [Test]
    public void TheFilterButton_SaysHowManyFiltersAreOn_ToAScreenReader() =>
        MainWindowFixture.Run((window, _) =>
        {
            var filter = (Button)window.FindName("ChainsFilterButton");

            Assert.That(System.Windows.Data.BindingOperations.GetBinding(filter, System.Windows.Automation.AutomationProperties.HelpTextProperty)!.Path.Path,
                Is.EqualTo("Chains.FilterHelpText"));
        });

    [Test]
    public void Esc_ClearsTheSearchBox_AndIsHandledThere() =>
        MainWindowFixture.Run((window, _) =>
        {
            var box = (TextBox)window.FindName("ChainsSearchBox");
            box.Text = "theseus";

            var escape = Key(box, System.Windows.Input.Key.Escape);

            Assert.That(escape.Handled, Is.True);
            Assert.That(box.Text, Is.Empty);
            Assert.That(Key(box, System.Windows.Input.Key.Escape).Handled, Is.True, "a second Esc goes to the tree, not to the window");
        });

    [Test]
    public void ThePanel_WithItsNewControls_HasNoUnnamedControl() =>
        MainWindowFixture.Run((window, shell) =>
        {
            var root = new ChainNodeViewModel("lead", "Lead", "#C0392B", string.Empty, false, string.Empty, string.Empty, new RelayCommand(_ => { }));
            root.Children.Add(new ChainNodeViewModel("child", "Child", "#C0392B", string.Empty, false, string.Empty, string.Empty, new RelayCommand(_ => { })));
            shell.Chains.Roots.Add(root);
            var pane = (FrameworkElement)window.FindName("ChainsPane");
            pane.Visibility = Visibility.Visible;
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(1400, 760));
            content.Arrange(new Rect(0, 0, 1400, 760));
            content.UpdateLayout();

            Assert.That(AutomationTree.Problems(pane), Is.Empty);
        });

    private static string AutomationName(UIElement element) => UIElementAutomationPeer.CreatePeerForElement(element).GetName();

    private static KeyEventArgs Key(UIElement target, Key key)
    {
        using HwndSource source = new(new HwndSourceParameters("chains-key-test") { Width = 1, Height = 1 });
        KeyEventArgs args = new(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        target.RaiseEvent(args);

        return args;
    }
}
