using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Theseus.Tests.Windows;

/// <summary>
/// Every icon-only button in the main window, as WPF builds it over a board with cards, has a name a screen reader can
/// say (baseline WB7; findings E9, C6; checklist G92, G167). An icon-font character is not a name: the button would
/// be read as an empty square.
/// </summary>
[TestFixture]
[NonParallelizable]
public class IconButtonNameTests
{
    [Test]
    public void EveryIconOnlyButton_HasAName() =>
        MainWindowFixture.Run(
            (window, _) =>
            {
                List<ButtonBase> icons = [.. MainWindowFixtureTests.Descendants<ButtonBase>((DependencyObject)window.Content).Where(IsIconOnly)];
                List<string> unnamed = [.. icons.Where(button => !IsSpoken(UIElementAutomationPeer.CreatePeerForElement(button)?.GetName()))
                    .Select(Describe)];

                Assert.That(icons, Has.Count.GreaterThan(10), "the walk found the board's and the panels' icon buttons");
                Assert.That(unnamed, Is.Empty);
            },
            board => board.With("alpha").With("beta", column: "In progress"));

    private static bool IsIconOnly(ButtonBase button) => button.Content switch
    {
        string text => IsGlyph(text),
        TextBlock block => IsGlyph(block.Text),
        _ => false,
    };

    private static bool IsGlyph(string? text) =>
        !string.IsNullOrEmpty(text) && text.All(character => character is >= '' and <= '');

    private static bool IsSpoken(string? name) => !string.IsNullOrWhiteSpace(name) && !IsGlyph(name);

    private static string Describe(ButtonBase button) =>
        $"{button.GetType().Name} '{button.Name}' tooltip '{button.ToolTip}' content U+{(int)((button.Content as string ?? (button.Content as TextBlock)?.Text ?? " ")[0]):X4}";
}
