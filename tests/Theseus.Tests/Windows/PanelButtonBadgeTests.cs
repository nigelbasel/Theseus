using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Theseus.Tests.Windows;

/// <summary>
/// A panel button's count sits over its icon's top-right corner, so the button is the icon's width whether or not a count
/// shows (Nigel, 2026-11-24: a hidden count left a wide gap beside the Locks and Chains icons).
/// </summary>
[TestFixture]
[NonParallelizable]
public class PanelButtonBadgeTests
{
    [Test]
    public void EachPanelButton_IsTheIconsWidth_WithItsCountOverTheCorner() =>
        MainWindowFixture.Run((window, _) =>
        {
            List<ToggleButton> panels = [.. ((ToolBar)window.FindName("MainToolBar")).Items.OfType<ToggleButton>()
                .Where(button => AutomationProperties.GetName(button) is "Live sessions panel" or "Locks panel" or "Chains panel")];

            Assert.That(panels, Has.Count.EqualTo(3));

            foreach (ToggleButton panel in panels)
            {
                Grid content = (Grid)panel.Content;
                Border badge = content.Children.OfType<Border>().Single();

                Assert.That((badge.HorizontalAlignment, badge.VerticalAlignment), Is.EqualTo((HorizontalAlignment.Right, VerticalAlignment.Top)),
                    AutomationProperties.GetName(panel));
                panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Assert.That(panel.DesiredSize.Width, Is.LessThanOrEqualTo(40), AutomationProperties.GetName(panel));
            }
        });
}
