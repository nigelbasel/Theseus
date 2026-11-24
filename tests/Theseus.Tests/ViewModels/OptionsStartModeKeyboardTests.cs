using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Launching;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Start mode is one Tab stop, and Tab lands on the choice in force, so tabbing through Options never changes it: a radio
/// is checked when it gets keyboard focus, and Options applies every change at once (W42, W6; review of WB4 PR 2).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class OptionsStartModeKeyboardTests
{
    [TestCase(FocusNavigationDirection.Next, "JiraChange")]
    [TestCase(FocusNavigationDirection.Previous, "PreReleases")]
    public void Tab_LandsOnTheChoiceInForce_AndLeavesItAlone(FocusNavigationDirection direction, string from)
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { DefaultStartMode = StartMode.Fresh } };
        SettingsViewModel panel = new(store, () => { });
        panel.OpenCommand.Execute(null);
        SettingsWindow window = new()
        {
            DataContext = panel, Left = -30000, Top = 0, ShowActivated = true, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual,
        };
        window.Show();

        try
        {
            window.UpdateLayout();
            ((UIElement)window.FindName(from)).Focus();

            ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(direction));

            Assert.That(Keyboard.FocusedElement, Is.SameAs(window.FindName("FreshChoice")), "the one Tab stop is the checked choice");
            Assert.That(store.Settings.DefaultStartMode, Is.EqualTo(StartMode.Fresh));
        }
        finally
        {
            window.Close();
        }
    }
}
