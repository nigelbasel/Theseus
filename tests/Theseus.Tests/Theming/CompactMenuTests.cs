using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Theseus.Tests.Theming;

/// <summary>
/// One choice of a set in a menu shows a bullet only while it is the chosen one, not a check box (C12, G54); a
/// flyout's edge takes its thickness from the theme, so a contrast theme draws it thicker (E7, G163); and the margins
/// Theseus tightened sit on the 4 px grid (D9, G109).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class CompactMenuTests
{
    private static ResourceDictionary Menus() => (ResourceDictionary)XamlReader.Parse(ThemeFiles.Text("CompactMenu.xaml"));

    [Test]
    public void ARadioItem_ShowsItsBullet_OnlyWhileChecked()
    {
        var item = new MenuItem { Header = "_Stage", IsCheckable = true, Template = (ControlTemplate)Menus()["CompactRadioItem"] };
        item.ApplyTemplate();
        var bullet = (FrameworkElement)item.Template.FindName("RadioBullet", item);
        var before = bullet.Visibility;

        item.IsChecked = true;

        Assert.That((before, bullet.Visibility), Is.EqualTo((Visibility.Hidden, Visibility.Visible)));
    }

    [Test]
    public void EveryFlyoutEdge_TakesItsThicknessFromTheTheme()
    {
        var text = ThemeFiles.Text("CompactMenu.xaml");

        Assert.That(Regex.Count(text, " Name=\"SubmenuBorder\""), Is.EqualTo(2));
        Assert.That(Regex.Count(text, "BorderThickness=\"\\{DynamicResource FlyoutBorderThickness\\}\""), Is.EqualTo(2));
    }

    [Test]
    public void TheMarginsTheseusTightened_NoLongerUseOffGridValues()
    {
        Assert.That(ThemeFiles.Text("CompactMenu.xaml"), Does.Not.Contain("Margin=\"1,2,1,2\"").And.Not.Contain("Margin=\"8,3,8,3\""));
    }
}
