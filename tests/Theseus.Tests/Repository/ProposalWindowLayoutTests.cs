using System.Windows;
using System.Windows.Controls;
using Theseus.App;
using Theseus.App.ViewModels;
using Theseus.Core.Proposals;

namespace Theseus.Tests.Repository;

/// <summary>
/// The proposal dialog's buttons as WPF lays them out: no access-key underscore drawn as text, and Windows order, together
/// at the bottom right with the default first and Close last (walkthrough row 46; A7).
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class ProposalWindowLayoutTests
{
    private static ProposalWindow Window()
    {
        var model = new ProposalWindowViewModel(
            new WorkstreamProposal("pr-1", ProposalStatus.Pending, "auth-research", "s",
                new ProposalRequest("Cache spike", "brief.md", true, null, "A verdict.", [], null), [], DateTimeOffset.UnixEpoch),
            "Auth research", "# Brief\n", _ => null, _ => null, (_, _) => null);
        var window = new ProposalWindow(model);

        // Under the Fluent theme, as the app runs (ThemeApplier): the default theme draws access keys, Fluent's did not.
        foreach (var source in new[] { "Fluent.xaml", "Fluent.Light.xaml" })
        {
            window.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"pack://application:,,,/PresentationFramework.Fluent;component/Themes/{source}"),
            });
        }

        var content = (FrameworkElement)window.Content;

        content.Measure(new Size(860, 720));
        content.Arrange(new Rect(0, 0, 860, 720));
        content.UpdateLayout();

        return window;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);

            if (child is T match)
            {
                yield return match;
            }

            foreach (var inner in Descendants<T>(child))
            {
                yield return inner;
            }
        }
    }

    [TestCase("ApproveButton", "Approve")]
    [TestCase("RejectButton", "Reject…")]
    public void AButton_DrawsItsAccessKey_NotAnUnderscore(string name, string label)
    {
        // The Approve button had the CommandButton style, whose template drew "_Approve" as text (walkthrough row 46).
        var button = (Button)Window().FindName(name);
        var accessText = Descendants<AccessText>(button).SingleOrDefault();

        Assert.That(Descendants<TextBlock>(button).Select(text => text.Text), Has.None.Contains("_"));
        Assert.That(accessText?.Text, Is.EqualTo("_" + label), "the label is an access key, Alt+" + label[0]);
    }

    [Test]
    public void CommandButton_DrawsAnAccessKey_NotAnUnderscore()
    {
        var resources = new ResourceDictionary { Source = new Uri("pack://application:,,,/Theseus;component/Themes/Interactive.xaml") };
        var button = new Button { Content = "_Go", Style = (Style)resources["CommandButton"] };
        var host = new Border { Child = button };

        host.Measure(new Size(200, 50));
        host.Arrange(new Rect(0, 0, 200, 50));
        host.UpdateLayout();

        Assert.That(Descendants<TextBlock>(button).Select(text => text.Text), Has.None.Contains("_"));
        Assert.That(Descendants<AccessText>(button).Single().Text, Is.EqualTo("_Go"));
    }

    [Test]
    public void TheButtons_AreTogetherAtTheRight_DefaultFirst_CloseLast()
    {
        var window = Window();
        var content = (FrameworkElement)window.Content;
        var buttons = new[] { "ApproveButton", "RejectButton", "CloseButton" }.Select(name => (Button)window.FindName(name)).ToList();
        var lefts = buttons.Select(button => button.TranslatePoint(new Point(0, 0), content).X).ToList();
        var closeRight = lefts[2] + buttons[2].ActualWidth;

        Assert.Multiple(() =>
        {
            Assert.That(lefts, Is.Ordered, "Approve, Reject…, Close, left to right");
            Assert.That(buttons[0].IsDefault, Is.True, "Approve is the default");
            Assert.That(buttons[2].IsCancel, Is.True);
            Assert.That(content.ActualWidth - 16 - closeRight, Is.LessThan(1), "Close ends at the right margin");
            Assert.That(lefts[2] - lefts[0], Is.LessThan(400), "together, not spread across the width");
        });
    }
}
