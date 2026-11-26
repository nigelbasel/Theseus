using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Theseus.Tests.Windows;

/// <summary>
/// A toolbar button that cannot run looks it, words and icon alike, and one that can lights under the pointer
/// (project-view-as-tabs, Nigel 2026-11-26: "start, stop and archive dont highlight when the mouse moves over them the
/// way the others do"). They were disabled, as a disabled button should not light, but their labels kept the primary
/// brush, so they looked live.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ToolbarDisabledLookTests
{
    [Test]
    public void EveryDisabledToolbarButton_DrawsAllItsTextInTheDisabledBrush() =>
        MainWindowFixture.Run((window, shell) =>
        {
            // The fixture loads App.xaml without a theme, so the theme's brushes are given here, each a colour of its own.
            Color disabled = Colors.Magenta;
            window.Resources["TextFillColorDisabledBrush"] = new SolidColorBrush(disabled);
            window.Resources["TextFillColorPrimaryBrush"] = new SolidColorBrush(Colors.Black);
            shell.CardCommandsCanRun = false;
            CommandManager();
            window.UpdateLayout();
            List<ButtonBase> off = [.. Buttons(window).Where(button => !button.IsEnabled)];
            List<string> live = [];

            Assert.That(off.Select(Name), Is.SupersetOf(new[] { "Start selected", "Stop selected", "Archive selected" }));

            foreach (ButtonBase button in off)
            {
                foreach (TextBlock text in Descendants<TextBlock>(button))
                {
                    if (text.Foreground is not SolidColorBrush { Color: var colour } || colour != disabled)
                    {
                        live.Add($"{Name(button)}: '{text.Text}'");
                    }
                }
            }

            Assert.That(live, Is.Empty);
        });

    [Test]
    public void EveryToolbarButton_ChangesItsFaceUnderThePointer() =>
        MainWindowFixture.Run((window, _) =>
        {
            List<string> unlit = [];

            foreach (ButtonBase button in Buttons(window))
            {
                bool lights = button.Template?.Triggers.OfType<Trigger>().Any(trigger =>
                    trigger.Property == UIElement.IsMouseOverProperty && true.Equals(trigger.Value)
                    && trigger.Setters.OfType<Setter>().Any(setter => setter.Property == Border.BackgroundProperty)) == true;

                if (!lights)
                {
                    unlit.Add(Name(button));
                }
            }

            Assert.That(unlit, Is.Empty);
        });

    private static void CommandManager() => System.Windows.Input.CommandManager.InvalidateRequerySuggested();

    private static string Name(ButtonBase button) => AutomationProperties.GetName(button);

    private static IEnumerable<ButtonBase> Buttons(Window window) =>
        ((ToolBar)window.FindName("MainToolBar")).Items.OfType<ButtonBase>().Where(button => button.Visibility == Visibility.Visible);

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (T deeper in Descendants<T>(child))
            {
                yield return deeper;
            }
        }
    }
}
