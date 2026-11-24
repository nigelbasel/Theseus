using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Theseus.Tests.Conventions;

namespace Theseus.Tests.Windows;

/// <summary>
/// Two rows of objective 1.5's walkthrough handed to this batch: the board's Show says when it hides cards (row 3), and
/// every Chains row has Focus, greyed with its reason where there is no session (row 12).
/// </summary>
[TestFixture]
[NonParallelizable]
public class WalkthroughRowsTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Test]
    public void Show_SaysWhichCardsItHides_WhileItHidesAny() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                FrameworkElement hides = (FrameworkElement)window.FindName("ShowHides");
                TextBlock words = (TextBlock)window.FindName("ShowHidesText");

                Assert.That(hides.Visibility, Is.EqualTo(Visibility.Collapsed), "nothing is hidden while Show is All");

                shell.Board.ShowsProjects = true;
                window.UpdateLayout();

                Assert.That(hides.Visibility, Is.EqualTo(Visibility.Visible));
                Assert.That(words.Text, Is.EqualTo("Workstreams hidden"), "a word, not colour alone (W46)");

                shell.Board.ShowsWorkstreams = true;
                window.UpdateLayout();

                Assert.That(words.Text, Is.EqualTo("Projects hidden"));
            },
            board => board.With("alpha"));

    [Test]
    public void EveryChainsRow_HasFocus_GreyedWithItsReason_WhenThereIsNoSession()
    {
        XElement focus = XDocument.Load(LooseXaml.SourceFile("MainWindow.xaml")).Descendants(Presentation + "Button")
            .Single(button => (string?)button.Attribute("Command") == "{Binding Focus}");

        Assert.That(focus.Attribute("Visibility"), Is.Null);
        Assert.That(focus.Descendants(Presentation + "Setter").Any(setter =>
            (string?)setter.Attribute("Property") == "Visibility"), Is.False, "never hidden: row 12 asked why some rows had none");
        Assert.That((string?)focus.Attribute("IsEnabled"), Is.EqualTo("{Binding HasFocus}"));
        Assert.That((string?)focus.Attribute("ToolTipService.ShowOnDisabled"), Is.EqualTo("True"));
        Assert.That(focus.Descendants(Presentation + "Trigger").Single().Elements(Presentation + "Setter")
            .Select(setter => (string?)setter.Attribute("Value")), Has.Some.StartsWith("No session to bring forward"));
    }
}
