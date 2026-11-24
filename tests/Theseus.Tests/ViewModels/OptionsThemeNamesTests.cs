using System.Windows;
using System.Windows.Controls;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Settings;

namespace Theseus.Tests.ViewModels;

/// <summary>The theme choice shows words, not the enum's names (D2, W17; checklist G142).</summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class OptionsThemeNamesTests
{
    [Test]
    public void TheChoices_AreNamedInWords()
    {
        SettingsViewModel panel = new(new MemorySettingsStore(), () => { });

        Assert.That(panel.ThemeChoices.Select(choice => choice.Label), Is.EqualTo(new[] { "Use system setting", "Light", "Dark" }));
    }

    [TestCase(AppTheme.System, "Use system setting")]
    [TestCase(AppTheme.Dark, "Dark")]
    public void TheValueLine_NamesTheThemeInWords(AppTheme theme, string expected)
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { Theme = theme } };

        Assert.That(new SettingsViewModel(store, () => { }).ThemeValue, Is.EqualTo(expected));
    }

    [Test]
    public void TheComboBox_ShowsUseSystemSetting_ForSystem()
    {
        MemorySettingsStore store = new() { Settings = TheseusSettings.Default with { Theme = AppTheme.System } };
        SettingsWindow window = new() { DataContext = new SettingsViewModel(store, () => { }) };
        FrameworkElement content = (FrameworkElement)window.Content;
        content.Measure(new Size(680, 2000));
        content.Arrange(new Rect(0, 0, 680, 2000));
        content.UpdateLayout();

        ComboBox box = (ComboBox)window.FindName("ThemeBox");

        Assert.That(box.Text, Is.EqualTo("Use system setting"));
    }
}
