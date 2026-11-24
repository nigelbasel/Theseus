using System.Windows;
using System.Windows.Controls;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Launching;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Start mode is one Tab stop, the choice in force, so tabbing through Options never changes it: a radio is checked when
/// it gets keyboard focus, and Options applies every change at once (W42, W6; review of WB4 PR 2). Asserted on the drawn
/// window's tab stops rather than by moving keyboard focus, which a CI runner without an interactive desktop cannot do.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class OptionsStartModeKeyboardTests
{
    [TestCase(StartMode.Fresh, "FreshChoice", "ResumeChoice")]
    [TestCase(StartMode.Resume, "ResumeChoice", "FreshChoice")]
    public void OnlyTheChoiceInForce_IsATabStop(StartMode mode, string stop, string skipped)
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { DefaultStartMode = mode } };
        SettingsWindow window = new() { DataContext = new SettingsViewModel(store, () => { }) };
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(680, 2000));
        content.Arrange(new Rect(0, 0, 680, 2000));
        content.UpdateLayout();

        Assert.That((((RadioButton)window.FindName(stop)).IsTabStop, ((RadioButton)window.FindName(skipped)).IsTabStop), Is.EqualTo((true, false)));
    }

    [Test]
    public void ChoosingTheOther_MovesTheTabStopWithIt()
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { DefaultStartMode = StartMode.Fresh } };
        SettingsViewModel panel = new(store, () => { });
        SettingsWindow window = new() { DataContext = panel };
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(680, 2000));
        content.Arrange(new Rect(0, 0, 680, 2000));

        panel.ResumeByDefault = true;
        content.UpdateLayout();

        Assert.That((((RadioButton)window.FindName("ResumeChoice")).IsTabStop, ((RadioButton)window.FindName("FreshChoice")).IsTabStop), Is.EqualTo((true, false)));
    }
}
